using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        // 実行ファイルが存在するディレクトリを取得
        string programDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // XML ファイルのパスを組み立てる
        string xmlFilePath = Path.Combine(programDirectory, "config.xml");

        // XML ファイルが存在するか確認
        if (File.Exists(xmlFilePath))
        {
            // XML ファイルが存在する場合は読み込む
            try
            {
                // XML ファイルを読み込み、XDocumentとして扱う
                XDocument xmlConfig = XDocument.Load(xmlFilePath);

                // フォルダパスを XML ファイルから取得
                string folderPath = xmlConfig.Root?.Element("TargetFolderPath")?.Value ?? null;
                if (folderPath == null)
                {
                    Console.WriteLine("指定されたフォルダは見つかりませんでした。");
                    return;
                }
                // 除外ファイルパスを XML ファイルから取得
                string[] IgnoreTables = xmlConfig.Root?.Element("IgnoreTables")?.Elements("Table").Select(e => e.Value).ToArray() ?? Array.Empty<string>();

                // 出力オプションを XML ファイルから取得
                string outputOption = xmlConfig.Root?.Element("OutputOption")?.Value ?? null;

                try
                {
                    int matchedFiles = 0;
                    int totalMatches = 0;

                    // フォルダ内のすべてのファイルに対して処理を行う
                    string[] filePaths = Directory.GetFiles(folderPath,"*",SearchOption.AllDirectories);

                    // フォルダ内のすべてのファイルに対して処理を行う
                    foreach (string filePath in filePaths)
                    {
                        if (Path.GetExtension(filePath).IndexOf(".sql", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // FileProcessor クラスを使ってファイルの内容を処理
                            FileProcessor.ProcessSqlFile(filePath, IgnoreTables, outputOption, ref matchedFiles, ref totalMatches);
                        }
                        else if (Path.GetExtension(filePath).IndexOf(".cs", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // FileProcessor クラスを使ってファイルの内容を処理
                            FileProcessor.ProcessCsFile(filePath, IgnoreTables, outputOption, ref matchedFiles, ref totalMatches);
                        }
                    }

                    Console.WriteLine($"ファイル件数: {filePaths.Length}");
                    Console.WriteLine($"合致したファイル件数: {matchedFiles}");
                    Console.WriteLine($"合致した箇所の件数: {totalMatches}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"エラーが発生しました: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"XML ファイルの読み込み中にエラーが発生しました: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("config.xml ファイルが見つかりませんでした。");
        }
    }
}
