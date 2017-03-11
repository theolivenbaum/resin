using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net.Config;
using Resin.Analysis;
using Resin.Querying;
using Sir.Client;

//TODO: move to own repo.
namespace Resin.Cli
{
    class Program
    {
        //inproc:
        // query --dir D:\resin\wikipedia -q "label:porn~" -p 0 -s 10
        // write --file c:\temp\0wikipedia.json --dir d:\resin\wikipedia --skip 0 --take 10000
        // delete --ids "Q1476435" --dir d:\resin\wikipedia
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (args[0].ToLower() == "write")
            {
                if (Array.IndexOf(args, "--file") == -1)
                {
                    Console.WriteLine("I need a file.");
                    return;
                }
                Write(args);
            }
            else if (args[0].ToLower() == "query")
            {
                if (Array.IndexOf(args, "-q") == -1)
                {
                    Console.WriteLine("I need a query.");
                    return;
                }
                Query(args);
            }
            else if (args[0].ToLower() == "delete")
            {
                Delete(args);
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe write --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
            }
        }

        static void Query(string[] args)
        {
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            var inproc = !string.IsNullOrWhiteSpace(dir);

            var q = args[Array.IndexOf(args, "-q") + 1];
            var page = 0;
            var size = 10;
            var url = ConfigurationManager.AppSettings.Get("sir.endpoint");
            int position;
            Result result;

            if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);
            if (Array.IndexOf(args, "--url") > 0) url = args[Array.IndexOf(args, "--url") + 1];

            var timer = new Stopwatch();
            timer.Start();

            if (inproc)
            {
                using (var s = new Searcher(dir, new QueryParser(new Analyzer()), new Tfidf()))
                {
                    result = s.Search(q, page, size);
                    var docs = result.Docs.ToList();

                    timer.Stop();

                    position = 0 + (page * size);

                    PrintHeaders();

                    foreach (var doc in docs)
                    {
                        Print(doc, position++);
                    }
                }
            }
            else
            {
                using (var s = new SearchClient(indexName, url))
                {
                    result = s.Search(q, page, size);
                    var docs = result.Docs.ToList();

                    timer.Stop();

                    position = 0 + (page * size);

                    PrintHeaders();

                    foreach (var doc in docs)
                    {
                        Print(doc, position++);
                    }
                }
            }

            Console.WriteLine("\r\n{0} results of {1} in {2}", position, result.Total, timer.Elapsed);  
        }

        private static void PrintHeaders()
        {
            Console.WriteLine();

            Console.WriteLine(string.Join(string.Empty,
                    string.Empty.PadRight(7),
                    "docid".PadRight(10),
                    "score".PadRight(10),
                    "label".PadRight(70),
                    "description"
                ));
            Console.WriteLine();
        }

        private static void Print(Document doc, int position)
        {
            Console.WriteLine(string.Join(string.Empty,
                            (position).ToString(CultureInfo.InvariantCulture).PadRight(7),
                            doc.Fields["_id"].ToString(CultureInfo.InvariantCulture).PadRight(10),
                            doc.Fields["__score"].ToString(CultureInfo.InvariantCulture).PadRight(10).Substring(0, 9).PadRight(10),
                            (doc.Fields["label"] ?? string.Empty).Substring(0, Math.Min(69, (doc.Fields["label"] ?? string.Empty).Length)).PadRight(70),
                            (doc.Fields["description"] ?? string.Empty).Substring(0, Math.Min(30, (doc.Fields["description"] ?? string.Empty).Length))
                        ));
        }

        static void Write(string[] args)
        {
            var take = 1000;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);

            var fileName = args[Array.IndexOf(args, "--file") + 1];
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            var url = ConfigurationManager.AppSettings.Get("sir.endpoint");
            var inproc = !string.IsNullOrWhiteSpace(dir);

            Console.WriteLine("writing...");

            var docs = new List<Dictionary<string, string>>();

            var writeTimer = new Stopwatch();
            writeTimer.Start();
          
            if (inproc)
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var writer = new StreamWriteOperation(dir, new Analyzer(), fileName, take))
                {
                    writer.Execute();
                }
            }
            else
            {
                Console.WriteLine("Executing HTTP POST");

                using (var client = new WriterClient(indexName, url))
                {
                    client.Write(docs);
                }
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }


        static void Delete(string[] args)
        {
            var ids = args[Array.IndexOf(args, "--ids") + 1].Split(',').Select(int.Parse);
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            var url = ConfigurationManager.AppSettings.Get("sir.endpoint");
            var inproc = !string.IsNullOrWhiteSpace(dir);
            var writeTimer = new Stopwatch();
            writeTimer.Start();

            if (inproc)
            {
                var analysisTimer = new Stopwatch();
                analysisTimer.Start();

                new DeleteOperation(dir, ids).Execute();
            }
            else
            {
                Console.WriteLine("Executing HTTP POST");

                using (var client = new WriterClient(indexName, url))
                {
                    client.Remove(ids);
                }
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }
    }
}
