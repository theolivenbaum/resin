using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net.Config;
using Newtonsoft.Json;
using Resin.Analysis;
using Resin.Client;
using Resin.Querying;

namespace Resin.Cli
{
    class Program
    {
        //query --dir D:\resin\wikipedia -q "label:porn~" -p 0 -s 10
        //write --file c:\temp\0wikipedia.json --dir d:\resin\wikipedia --skip 0 --take 10000
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
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe write --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
                Console.WriteLine("rn.exe analyze --dir c:\\my_index -field field_name");
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
            var url = ConfigurationManager.AppSettings.Get("resin.endpoint");

            if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);
            if (Array.IndexOf(args, "--url") > 0) url = args[Array.IndexOf(args, "--url") + 1];

            var timer = new Stopwatch();
            timer.Start();

            if (inproc)
            {
                using (var s = new Searcher(dir, new QueryParser(new Analyzer()), new Tfidf()))
                {
                    var result = s.Search(q, page, size);
                    var docs = result.Docs.ToList();

                    timer.Stop();

                    var position = 0 + (page * size);

                    Console.WriteLine();
                    Console.WriteLine(string.Join(string.Empty,
                            string.Empty.PadRight(7),
                            "docid".PadRight(10),
                            "score".PadRight(10),
                            "label".PadRight(70),
                            "description"
                        ));
                    Console.WriteLine();

                    foreach (var doc in docs)
                    {
                        Console.WriteLine(string.Join(string.Empty,
                            (++position).ToString(CultureInfo.InvariantCulture).PadRight(7),
                            doc.Fields["_id"].ToString(CultureInfo.InvariantCulture).PadRight(10),
                            doc.Fields["__score"].ToString(CultureInfo.InvariantCulture).PadRight(10).Substring(0, 9).PadRight(10),
                            (doc.Fields["label"] ?? string.Empty).Substring(0, Math.Min(69, (doc.Fields["label"] ?? string.Empty).Length)).PadRight(70),
                            (doc.Fields["description"] ?? string.Empty).Substring(0, Math.Min(30, (doc.Fields["description"] ?? string.Empty).Length))
                        ));
                    }

                    Console.WriteLine("\r\n{0} results of {1} in {2}", position, result.Total, timer.Elapsed);  
                }
                
            }
            //else
            //{
            //    using (var s = new SearchClient(indexName, url))
            //    {
            //        var result = s.Search(q, page, size);
            //        var docs = result.Docs.ToList();
            //        timer.Stop();
            //        var position = 0 + (page * size);
            //        foreach (var doc in docs)
            //        {
            //            Console.WriteLine(string.Join(", ", ++position, doc["_id"], doc["label"]));
            //        }
            //        Console.WriteLine("\r\n{0} results of {1} in {2} ms", position, result.Total, timer.Elapsed.TotalMilliseconds);
            //    }
            //}

        }

        static void Write(string[] args)
        {
            var take = 1000;
            var skip = 0;
            var skipped = 0;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);

            var fileName = args[Array.IndexOf(args, "--file") + 1];
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            var url = ConfigurationManager.AppSettings.Get("resin.endpoint");
            var inproc = !string.IsNullOrWhiteSpace(dir);

            IndexWriter w = inproc ? new IndexWriter(dir, new Analyzer()) : null;

            Console.Write("Preparing docs: ");

            var cursorPos = Console.CursorLeft;
            var count = 0;
            var docs = new List<Dictionary<string, string>>();

            var timer = new Stopwatch();
            timer.Start();

            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var bs = new BufferedStream(fs))
            using (var sr = new StreamReader(bs))
            {
                string line;

                sr.ReadLine();

                while (skipped++ < skip)
                {
                    sr.ReadLine();
                }

                while ((line = sr.ReadLine()) != null)
                {
                    if (line[0] == ']') break;

                    var doc = JsonConvert.DeserializeObject<Dictionary<string, string>>(line.Substring(0, line.Length - 1));

                    Console.SetCursorPosition(cursorPos, Console.CursorTop);
                    Console.Write(++count);

                    docs.Add(doc);

                    if (count == take) break;
                }
                Console.WriteLine();
            }

            Console.WriteLine("collected docs in {0}", timer.Elapsed);

            if (inproc)
            {
                w.Write(docs.Select(d=>new Document(d)));

                w.Dispose();
            }
            else
            {
                Console.WriteLine("Executing HTTP POST");

                using (var client = new WriterClient(indexName, url))
                {
                    client.Write(docs);
                }
            }
            
        }
    }
}
