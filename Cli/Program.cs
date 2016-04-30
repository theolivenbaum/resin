using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net.Config;
using Newtonsoft.Json;
using Resin.Client;

namespace Resin.Cli
{
    class Program
    {
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
            //else if (args[0].ToLower() == "optimize")
            //{
            //    if (Array.IndexOf(args, "--dir") == -1)
            //    {
            //        Console.WriteLine("I need a directory.");
            //        return;
            //    }
            //    Optimize(args);
            //}
            else if (args[0].ToLower() == "remove")
            {
                if (Array.IndexOf(args, "--docid") == -1)
                {
                    Console.WriteLine("I need a doc id.");
                    return;
                }
                Remove(args);
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
            //else if (args[0].ToLower() == "analyze")
            //{
            //    if (Array.IndexOf(args, "--field") == -1 ||
            //        Array.IndexOf(args, "--dir") == -1)
            //    {
            //        Console.WriteLine("I need a directory and a field.");
            //        return;
            //    }
            //    Analyze(args);
            //}
            else if (args[0].ToLower() == "about")
            {
                About();
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe write --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
                Console.WriteLine("rn.exe analyze --dir c:\\my_index -field field_name");
                Console.WriteLine("rn.exe about");
            }
        }

        static void About()
        {
            //var about = File.ReadAllText(@"..\..\..\readme.md");
            //var dir = Path.Combine(Environment.CurrentDirectory, "about");
            //using (var writer = new IndexWriter(dir, new Analyzer()))
            //{
            //    writer.Write(new Dictionary<string, string>
            //            {
            //                {"body", about}
            //            }
            //);
            //}
            //var scanner = FieldScanner.MergeLoad(dir);
            //var timer = new Stopwatch();
            //timer.Start();
            //var tokens = scanner.GetAllTokens("body").OrderByDescending(t => t.Count).ToList();
            //Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
            //File.WriteAllLines(Path.Combine(dir, "_about.txt"), tokens.Select(t => string.Format("{0} {1}", t.Token, t.Count)));
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
                var s = new Searcher(dir, new QueryParser(new Analyzer()));
                var result = s.Search(q, page, size);
                var docs = result.Docs.ToList();
                timer.Stop();
                var position = 0 + (page * size);
                Console.WriteLine();
                Console.WriteLine(string.Join(string.Empty,
                        string.Empty.PadRight(7),
                        "docid".PadRight(10),
                        "label".PadRight(50),
                        "aliases"
                    ));
                Console.WriteLine();
                foreach (var doc in docs)
                {
                    Console.WriteLine(string.Join(string.Empty, 
                        (++position).ToString(CultureInfo.InvariantCulture).PadRight(7),
                        doc["_id"].ToString(CultureInfo.InvariantCulture).PadRight(10),
                        (doc["label"] ?? string.Empty).Substring(0, Math.Min(49, (doc["label"] ?? string.Empty).Length)).PadRight(50),
                        (doc["aliases"] ?? string.Empty).Substring(0, Math.Min(100, (doc["aliases"] ?? string.Empty).Length))
                    ));
                }
                Console.WriteLine("\r\n{0} results of {1} in {2}", position, result.Total, timer.Elapsed);
            }
            else
            {
                using (var s = new SearchClient(indexName, url))
                {
                    var result = s.Search(q, page, size);
                    var docs = result.Docs.ToList();
                    timer.Stop();
                    var position = 0 + (page * size);
                    foreach (var doc in docs)
                    {
                        Console.WriteLine(string.Join(", ", ++position, doc["_id"], doc["label"]));
                    }
                    Console.WriteLine("\r\n{0} results of {1} in {2} ms", position, result.Total, timer.Elapsed.TotalMilliseconds);
                } 
            }
            
        }

        //static void Analyze(string[] args)
        //{
        //    var dir = args[Array.IndexOf(args, "--dir") + 1];
        //    var field = args[Array.IndexOf(args, "--field") + 1];
        //    var timer = new Stopwatch();
        //    timer.Start();
        //    var fileName = Path.Combine(dir, field.ToHash() + ".tr");
        //    var trie = Trie.Load(fileName);
        //    var tokens = trie.All().Take(100).ToList();
        //    Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
        //    File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), tokens);
        //}

        //static void Optimize(string[] args)
        //{
        //    var dir = args[Array.IndexOf(args, "--dir") + 1];
        //    var truncate = Array.IndexOf(args, "--truncate") > 0;
        //    var timer = new Stopwatch();
        //    timer.Start();
        //    var ixFileName = Helper.GetFileNameOfLatestIndex(dir);
        //    var ix = IxFile.Load(ixFileName);
        //    var dix = DixFile.Load(Path.Combine(dir, ix.DixFileName));
        //    var fix = FixFile.Load(Path.Combine(dir, ix.FixFileName));

        //    var optimizer = new Optimizer(dir, ixFileName, dix, fix);
        //    optimizer.Rebase();
        //    if (truncate) optimizer.Truncate();
        //    optimizer.Save();
        //}

        static void Remove(string[] args)
        {
            string dir = null;
            string indexName = null;
            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];
            var inproc = !string.IsNullOrWhiteSpace(dir);
            var docId = args[Array.IndexOf(args, "--docid") + 1];
            var timer = new Stopwatch();
            timer.Start();
            if (inproc)
            {
                using (var writer = new IndexWriter(dir, new Analyzer()))
                {
                    writer.Remove(docId);
                }
            }
            else
            {
                var url = ConfigurationManager.AppSettings.Get("resin.endpoint");
                using (var client = new WriterClient(indexName, url))
                {
                    client.Remove(docId);
                }
            }
            Console.WriteLine("deleted {0} in {1}", docId, timer.Elapsed);
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

            Console.Write(inproc ? "Writing " : "Collecting docs ");

            var cursorPos = Console.CursorLeft;
            var count = 0;
            var docs = new List<Dictionary<string, string>>();
            var timer = new Stopwatch();
            timer.Start();
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

                    if (inproc)
                    {
                        w.Write(doc);
                    }
                    else
                    {
                        docs.Add(doc);
                    }

                    if (count == take) break;
                }
                Console.WriteLine();
            }

            if (inproc)
            {
                w.Dispose();
            }
            else
            {
                Console.Write("Executing HTTP POST");
                using (var client = new WriterClient(indexName, url))
                {
                    client.Write(docs);
                }
            }
            Console.Write("total time elapsed {0}", timer.Elapsed);
        }
    }
}
