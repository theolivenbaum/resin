using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Resin
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args[0].ToLower() == "write")
            {
                if (Array.IndexOf(args, "--file") == -1 ||
                    Array.IndexOf(args, "--dir") == -1)
                {
                    Console.WriteLine("I need a file and a directory.");
                    return;
                }
                Write(args);
            }
            else if (args[0].ToLower() == "query")
            {
                if (Array.IndexOf(args, "-q") == -1 ||
                    Array.IndexOf(args, "--name") == -1)
                {
                    Console.WriteLine("I need an index name and a query.");
                    return;
                }
                Query(args);
            }
            else if (args[0].ToLower() == "analyze")
            {
                if (Array.IndexOf(args, "--field") == -1 || 
                    Array.IndexOf(args, "--dir") == -1)
                {
                    Console.WriteLine("I need a directory and a field.");
                    return;
                }
                Analyze(args);
            }
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
            var about = File.ReadAllText(@"..\..\..\readme.md");
            var dir = Path.Combine(Environment.CurrentDirectory, "about");
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(new Dictionary<string, string>
                        {
                            {"body", about}
                        }
            );
            }
            var scanner = FieldScanner.MergeLoad(dir);
            var timer = new Stopwatch();
            timer.Start();
            var tokens = scanner.GetAllTokens("body").OrderByDescending(t => t.Count).ToList();
            Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
            File.WriteAllLines(Path.Combine(dir, "_about.txt"), tokens.Select(t => string.Format("{0} {1}", t.Token, t.Count)));
        }

        static void Query(string[] args)
        {
            var name = args[Array.IndexOf(args, "--name") + 1];
            var q = args[Array.IndexOf(args, "-q") + 1];
            var page = 0;
            var size = 10;
            var url = "http://localhost:1234/";
            if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);
            if (Array.IndexOf(args, "--url") > 0) url = args[Array.IndexOf(args, "--url") + 1];
            var timer = new Stopwatch();
            using (var s = new SearchClient(name, url))
            {              
                timer.Start();
                var result = s.Search(q, page, size);
                timer.Stop();
                var position = 0 + (page * size);
                foreach (var doc in result.Docs)
                {
                    Console.WriteLine(string.Join(", ", ++position, doc["_id"], doc["label"]));
                    //Console.ForegroundColor = ConsoleColor.DarkCyan;
                    //Console.Write(" {0}", result.Trace[doc["_id"].ToLower()]);
                    //Console.ResetColor();
                    //Console.WriteLine();
                }
                Console.WriteLine("\r\n{0} results of {1} in {2} ms", position, result.Total, timer.Elapsed.TotalMilliseconds);
            }
        }

        static void Analyze(string[] args)
        {
            var dir = args[Array.IndexOf(args, "--dir") + 1];
            var field = args[Array.IndexOf(args, "--field") + 1];
            var timer = new Stopwatch();
            timer.Start();
            var scanner = FieldScanner.MergeLoad(dir);
            var tokens = scanner.GetAllTokens(field).OrderByDescending(t => t.Count).ToList();
            var trieTokens = scanner.GetAllTokensFromTrie(field);
            Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
            File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), tokens.Select(t => string.Format("{0} {1}", t.Token, t.Count)));
            File.WriteAllLines(Path.Combine(dir, "_" + field + ".tri.txt"), trieTokens);
        }

        static void Write(string[] args)
        {
            Console.Write("Writing: ");

            var take = 1000;
            var skip = 0;

            var skipped = 0;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);

            var fileName = args[Array.IndexOf(args, "--file") + 1];
            var dir = args[Array.IndexOf(args, "--dir") + 1];
            var cursorPos = Console.CursorLeft;
            var count = 0;
            var stopwords = File.ReadAllLines("stopwords.txt");
            var timer = new Stopwatch();
            timer.Start();
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream(fs))
            using (var sr = new StreamReader(bs))
            using (var indexWriter = new IndexWriter(dir, new Analyzer(stopwords: stopwords)))
            {
                string line = sr.ReadLine();
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
                    indexWriter.Write(doc);

                    if (count == take) break;
                }
            }
            Console.WriteLine("\r\nIndex created in " + timer.Elapsed);
        }
    }
}
