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
                XDocument doc = XDocument.Load(xmlFilePath);

                // ConfigクラスにXMLの内容を格納
                Config config = new Config
                {
                    Targets = doc.Descendants("Target").Select(target => new Target
                    {
                        FolderPath = (string)target.Element("FolderPath"),
                        ResultFilePath = (string)target.Element("ResultFilePath")
                    }).ToList(),
                    IgnoreTables = new IgnoreTables
                    {
                        Tables = doc.Descendants("IgnoreTables").Elements("Table").Select(table => (string)table).ToList()
                    },
                    OutputOption = (string)doc.Descendants("OutputOption").FirstOrDefault()
                };


                try
                {
                    // FileProcessorクラスをインスタンス化し、Configを渡して処理を実行
                    FileProcessor fileProcessor = new FileProcessor(config);
                    fileProcessor.ProcessFiles();
                    
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
