using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class FileProcessor
{
    internal static void ProcessSqlFile(string filePath, string[] ignoreTables, string outputOption, ref int matchedFiles, ref int totalMatches)
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
                    OutputResult(filePath, lineNumber, GetSubstringAfterStart(currentString, "SELECT"), outputOption);
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
    private static string ReplaceSqlComments(string input)
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
    private static bool MatchesExcludePatterns(string text, string[] excludePatterns)
    {
        foreach (string pattern in excludePatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true; // 一致する場合は除外
            }
        }
        return false; // 一致しない場合は通常処理
    }
    // 指定されたインデックスの行番号を取得する
    private static int GetLineNumber(string text, int index)
    {
        string[] lines = text.Substring(0, index).Split('\n');
        return lines.Length;
    }
    static string GetSubstringAfterStart(string inputString, string startSubstring)
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

    internal static void ProcessCsFile(string filePath, string[] ignoreTables, string outputOption, ref int matchedFiles, ref int totalMatches)
    {
        // ファイルの内容を読み込む
        string fileContent = File.ReadAllText(filePath);
        bool matchedCondition = false;

        SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
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

                    OutputResult(filePath, firstLineNumber, GetCodeSnippet(fileContent, firstLineNumber, lastLineNumber), outputOption);
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
    static string GetCodeSnippet(string sourceCode, int startLine, int endLine)
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
    static void OutputResult(string filePath, int lineNumber, string contents, string outputFormat)
    {
        string result = contents;

        // 出力形式によって処理を変更
        if (outputFormat == "csv")
        {
            // 改行を削除
            result = result.Replace("\n", "").Replace("\r", "");
            // 文字列、文字列の行番号を出力
            Console.WriteLine($"{filePath},{lineNumber},\"{EscapeDoubleQuotes(result)}\"");
        }
        else if (outputFormat == "tsv")
        {
            // 改行を削除
            result = result.Replace("\n", "").Replace("\r", "");
            // 文字列、文字列の行番号を出力
            Console.WriteLine($"{filePath}\t{lineNumber}\t\"{result}\"");
        }
        else
        {
            // 文字列、文字列の行番号を出力
            Console.WriteLine($"ファイル名: {filePath}({lineNumber})");
            Console.WriteLine($"一致箇所: {contents}");
        }
    }
    static string EscapeDoubleQuotes(string input)
    {
        // ダブルクォーテーションを2つ続けることでエスケープ
        return input.Replace("\"", "\"\"");
    }
}
