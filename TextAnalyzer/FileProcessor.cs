﻿using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.VisualBasic;

class FileProcessor
{
    private Config config;

    public FileProcessor(Config config)
    {
        this.config = config;
    }
    public void ProcessFiles()
    {
        foreach (var target in config.Targets)
        {
            int matchedFiles = 0;
            int totalMatches = 0;

            // フォルダ内のすべてのファイルに対して処理を行う
            string[] filePaths = Directory.GetFiles(target.FolderPath, "*", SearchOption.AllDirectories);

            // フォルダ内のすべてのファイルに対して処理を行う
            foreach (string filePath in filePaths)
            {
                if (Path.GetExtension(filePath).IndexOf(".sql", StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetExtension(filePath).IndexOf(".txt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // FileProcessor クラスを使ってファイルの内容を処理
                    ProcessSqlFile(filePath, config.IgnoreTables.Tables, config.OutputOption, target.ResultFilePath, ref matchedFiles, ref totalMatches);
                }
                else if (Path.GetExtension(filePath).IndexOf(".cs", StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetExtension(filePath).IndexOf(".vb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // FileProcessor クラスを使ってファイルの内容を処理
                    ProcessCsVbFile(filePath, config.IgnoreTables.Tables, config.OutputOption, target.ResultFilePath, ref matchedFiles, ref totalMatches);
                }
            }

            WriteToFile($"ファイル件数: {filePaths.Length}", target.ResultFilePath);
            WriteToFile($"合致したファイル件数: {matchedFiles}", target.ResultFilePath);
            WriteToFile($"合致した箇所の件数: {totalMatches}", target.ResultFilePath);
        }
    }
    private void ProcessSqlFile(string filePath, List<string> ignoreTables, string outputOption, string resultFilePath, ref int matchedFiles, ref int totalMatches)
    {
        try
        {
            // ファイルの内容を読み込む
            string fileContent = File.ReadAllText(filePath);
            // SQLコメントを特定の文字列に置き換える
            fileContent = ReplaceSqlComments(fileContent);
            bool matchedCondition = false;
            // 正規表現パターン
            string pattern = @"[^;]+(?=;)";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase); // 大文字小文字を無視する

            // 正規表現にマッチする文字列を抽出
            MatchCollection matches = regex.Matches(fileContent);

            // 各マッチング文字列について処理を行う
            foreach (Match match in matches)
            {
                string currentString = match.Value;

                // 条件をチェック
                if (currentString.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase) >= 0
                    && currentString.IndexOf("INSERT", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("INTO", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("ORDER", StringComparison.OrdinalIgnoreCase) < 0
                    && !MatchesExcludePatterns(currentString, ignoreTables))
                {
                    // 文字列、文字列の行番号を出力
                    int lineNumber = GetLineNumber(fileContent, match.Index + currentString.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase));
                    //Console.WriteLine($"ファイル名: {filePath}({lineNumber})");
                    //Console.WriteLine($"一致箇所: {GetSubstringAfterStart(currentString, "SELECT")}");
                    OutputResult(filePath, lineNumber, GetSubstringAfterStart(currentString, "SELECT"), outputOption, resultFilePath);
                    // 合致した箇所の総数をインクリメント
                    totalMatches++;
                    matchedCondition = true;
                }
            }

            // 合致したファイルの数をインクリメント
            if (matchedCondition)
            {
                matchedFiles++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラーが発生しました: {ex.Message}");
        }
    }
    // SQLコメントは検索対象外の為、特定の文字列に置き換える
    private string ReplaceSqlComments(string input)
    {
        // 正規表現パターン: /* ～ */
        string blockCommentPattern = @"/\*.*?\*/";
        // 正規表現パターン2: -- ～ 改行
        string lineCommentPattern = @"--.*?$";

        string result = Regex.Replace(input, blockCommentPattern, match => new string('@', match.Length));
        result = Regex.Replace(result, lineCommentPattern, match => new string('@', match.Length));

        return result;

    }
    // 除外条件に一致するかチェック
    private bool MatchesExcludePatterns(string text, List<string> excludePatterns)
    {
        foreach (string pattern in excludePatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase) || CountString(text, "from") == 1)
            {
                return true; // 一致する場合は除外
            }
        }
        return false; // 一致しない場合は通常処理
    }
    // 指定されたインデックスの行番号を取得する
    private int GetLineNumber(string text, int index)
    {
        string[] lines = text.Substring(0, index).Split('\n');
        return lines.Length;
    }
    private string GetSubstringAfterStart(string inputString, string startSubstring)
    {
        int startIndex = inputString.IndexOf(startSubstring, StringComparison.OrdinalIgnoreCase);
        if (startIndex != -1)
        {
            string result = inputString.Substring(startIndex);
            return result;
        }
        else
        {
            return null;
        }
    }
    private int CountString(string search, string target)
    {
        int cnt = 0;
        bool check = true;

        while (check)
        {
            if (target.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) == -1)
            {
                check = false;
            }
            else
            {
                target = target.Remove(0, target.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) + 1);
                cnt++;
            }
        }

        return cnt;
    }

    private void ProcessCsVbFile(string filePath, List<string> ignoreTables, string outputOption, string resultFilePath, ref int matchedFiles, ref int totalMatches)
    {
        // ファイルの内容を読み込む
        string fileContent = File.ReadAllText(filePath);
        bool matchedCondition = false;

        SyntaxTree tree;
        if (Path.GetExtension(filePath).IndexOf(".vb", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            tree = VisualBasicSyntaxTree.ParseText(fileContent);
        }
        else
        {
            tree = CSharpSyntaxTree.ParseText(fileContent);
        }

        var root = (CompilationUnitSyntax)tree.GetRoot();

        var methodNodes = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var methodNode in methodNodes)
        {
            //Console.WriteLine($"Method: {methodNode.Identifier}");

            var appendOrAppendLineCalls = methodNode.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                     (memberAccess.Name.Identifier.Text == "Append" || memberAccess.Name.Identifier.Text == "AppendLine"))
                .ToList();

            if (appendOrAppendLineCalls.Any())
            {
                StringBuilder combinedArguments = new StringBuilder();

                foreach (var call in appendOrAppendLineCalls)
                {
                    var argument = call.ArgumentList.Arguments.First();
                    combinedArguments.Append(argument.ToString());
                }
  
                string currentString = combinedArguments.ToString();
                if (currentString.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase) >= 0
                    && currentString.IndexOf("INSERT", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("INTO", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("ORDER", StringComparison.OrdinalIgnoreCase) < 0
                    && !MatchesExcludePatterns(currentString, ignoreTables))
                {
                    var firstCall = appendOrAppendLineCalls.First();
                    var lastCall = appendOrAppendLineCalls.Last();
                    var firstLineNumber = tree.GetLineSpan(firstCall.Span).StartLinePosition.Line + 1;
                    var lastLineNumber = tree.GetLineSpan(lastCall.Span).StartLinePosition.Line + 1;

                    // 文字列、文字列の行番号を出力
                    //Console.WriteLine($"ファイル名: {filePath}({firstLineNumber})");
                    //Console.WriteLine($"一致箇所: {GetCodeSnippet(fileContent, firstLineNumber, lastLineNumber)}");

                    OutputResult(filePath, firstLineNumber, GetCodeSnippet(fileContent, firstLineNumber, lastLineNumber), outputOption, resultFilePath);
                    // 合致した箇所の総数をインクリメント
                    totalMatches++;
                    matchedCondition = true;
                }
            }
        }
        // 合致したファイルの数をインクリメント
        if (matchedCondition)
        {
            matchedFiles++;
        }
    }
    private void ProcessVbFile(string filePath, List<string> ignoreTables, string outputOption, string resultFilePath, ref int matchedFiles, ref int totalMatches)
    {
        // ファイルの内容を読み込む
        string fileContent = File.ReadAllText(filePath);
        bool matchedCondition = false;

        SyntaxTree tree = VisualBasicSyntaxTree.ParseText(fileContent);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        var methodNodes = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockSyntax>();
        foreach (var methodNode in methodNodes)
        {
            var appendOrAppendLineCalls = methodNode.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                     (memberAccess.Name.Identifier.Text == "Append" || memberAccess.Name.Identifier.Text == "AppendLine"))
                .ToList();

            if (appendOrAppendLineCalls.Any())
            {
                StringBuilder combinedArguments = new StringBuilder();

                foreach (var call in appendOrAppendLineCalls)
                {
                    var argument = call.ArgumentList.Arguments.First();
                    combinedArguments.Append(argument.ToString());
                }

                string currentString = combinedArguments.ToString();
                if (currentString.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase) >= 0
                    && currentString.IndexOf("INSERT", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("INTO", StringComparison.OrdinalIgnoreCase) < 0
                    && currentString.IndexOf("ORDER", StringComparison.OrdinalIgnoreCase) < 0
                    && !MatchesExcludePatterns(currentString, ignoreTables))
                {
                    var firstCall = appendOrAppendLineCalls.First();
                    var lastCall = appendOrAppendLineCalls.Last();
                    var firstLineNumber = tree.GetLineSpan(firstCall.Span).StartLinePosition.Line + 1;
                    var lastLineNumber = tree.GetLineSpan(lastCall.Span).StartLinePosition.Line + 1;

                    // 文字列、文字列の行番号を出力
                    //Console.WriteLine($"ファイル名: {filePath}({firstLineNumber})");
                    //Console.WriteLine($"一致箇所: {GetCodeSnippet(fileContent, firstLineNumber, lastLineNumber)}");

                    OutputResult(filePath, firstLineNumber, GetCodeSnippet(fileContent, firstLineNumber, lastLineNumber), outputOption, resultFilePath);
                    // 合致した箇所の総数をインクリメント
                    totalMatches++;
                    matchedCondition = true;
                }
            }
        }
        // 合致したファイルの数をインクリメント
        if (matchedCondition)
        {
            matchedFiles++;
        }
    }
    private string GetCodeSnippet(string sourceCode, int startLine, int endLine)
    {
        string[] lines = sourceCode.Split('\n');

        // Adjust startLine and endLine to be within the valid range
        startLine = Math.Max(0, Math.Min(startLine - 1, lines.Length - 1));
        endLine = Math.Max(0, Math.Min(endLine - 1, lines.Length - 1));

        // Extract the specified lines
        string[] selectedLines = new ArraySegment<string>(lines, startLine, endLine - startLine + 1).ToArray();

        // Join the selected lines to form the code snippet
        string selectedCode = string.Join(Environment.NewLine, selectedLines);

        return selectedCode;
    }
    private void OutputResult(string filePath, int lineNumber, string contents, string outputFormat, string resultFilePath)
    {
        string result = contents;

        // 出力形式によって処理を変更
        if (outputFormat == "csv")
        {
            // 改行を削除
            result = result.Replace("\n", "").Replace("\r", "");
            // 文字列、文字列の行番号を出力
            WriteToFile($"{filePath},{lineNumber},\"{EscapeDoubleQuotes(result)}\"", resultFilePath);
        }
        else if (outputFormat == "tsv")
        {
            // 改行を削除
            result = result.Replace("\n", "").Replace("\r", "");
            // 文字列、文字列の行番号を出力
            WriteToFile($"{filePath}\t{lineNumber}\t\"{result}\"", resultFilePath);
           
        }
        else
        {

            WriteToFile($"ファイル名: {filePath}({lineNumber})", resultFilePath);
            WriteToFile($"一致箇所: {contents}", resultFilePath);
 
        }
    }
    private string EscapeDoubleQuotes(string input)
    {
        // ダブルクォーテーションを2つ続けることでエスケープ
        return input.Replace("\"", "\"\"");
    }

    private void WriteToFile(string content , string filePath)
    {
        if (filePath == null)
        {
            // 文字列、文字列の行番号を出力
            Console.WriteLine(content);
        }
        else
        {
            // ファイルに書き込む
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine(content);
            }
        }
        
    }

}
class Target
{
    public string FolderPath { get; set; }
    public string ResultFilePath { get; set; }
}

class IgnoreTables
{
    public List<string> Tables { get; set; }
}

class Config
{
    public List<Target> Targets { get; set; }
    public IgnoreTables IgnoreTables { get; set; }
    public string OutputOption { get; set; }
}