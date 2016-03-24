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
                    Console.WriteLine("I need both a file and a directory.");
                    return;
                }
                var take = 1000;
                var skip = 0;
                if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
                if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);
                var fileName = args[Array.IndexOf(args, "--file") + 1];
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var json = File.ReadAllText(fileName);
                var docs = JsonConvert.DeserializeObject<List<Document>>(json).Skip(skip).Take(take).ToList();
                Console.Write("Writing: ");
                var cursorPos = Console.CursorLeft;
                var count = 0;
                var timer = new Stopwatch();
                timer.Start();
                using (var w = new IndexWriter(dir, new Analyzer(stopwords:new []{"the", "a", "an", "to", "of", "on", "and", "that", "in", "it", "is", "be", "as", "if", "we", "at", "for"})))
                {
                    foreach (var d in docs)
                    {
                        Console.SetCursorPosition(cursorPos, Console.CursorTop);
                        Console.Write(++count);
                        w.Write(d);
                    }
                }
                timer.Stop();
                Console.WriteLine("");
                Console.WriteLine("Index created in " + timer.Elapsed);
            }
            else if (args[0].ToLower() == "query")
            {
                if (Array.IndexOf(args, "-q") == -1 ||
                    Array.IndexOf(args, "--dir") == -1)
                {
                    Console.WriteLine("I need both a directory and a query.");
                    return;
                }

                var total = new Stopwatch();
                total.Start();

                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var q = args[Array.IndexOf(args, "-q") + 1];
                var page = 0;
                var size = 10;
                if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
                if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);
                var timer = new Stopwatch();
                timer.Start();
                using (var s = new Searcher(dir))
                {
                    Console.WriteLine("\r\nDocument index loaded in {0} ms", timer.ElapsedMilliseconds);

                    timer.Restart();
                    for (int i = 0; i < 1; i++)
                    {
                        s.Search(q, page, size); // warm up the "label" field
                    }
                    Console.WriteLine("Term index loaded in {0} ms", timer.ElapsedMilliseconds);

                    timer.Restart();
                    var result = s.Search(q, page, size);
                    var docs = result.Docs;
                    Console.WriteLine("Scan and scoring in {0}\r\n", timer.Elapsed);
                    var position = 0+(page*size);
                    timer.Restart();
                    foreach (var doc in docs)
                    {
                        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
                    }
                    Console.WriteLine("\r\n{0} results of {1}\r\nDocs loaded from disk in {2} ms", position, result.Total, timer.ElapsedMilliseconds);
                }
                Console.WriteLine("Total time elapsed: {0}", total.Elapsed);

            }
            else if (args[0].ToLower() == "analyze")
            {
                if (Array.IndexOf(args, "--field") == -1 || Array.IndexOf(args, "--dir") == -1)
                {
                    Console.WriteLine("I need a directory and a field.");
                    return;
                }
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var field = args[Array.IndexOf(args, "--field") + 1];
                var timer = new Stopwatch();
                timer.Start();
                var scanner = new Scanner(dir);
                var tokens = scanner.GetAllTokens(field).OrderByDescending(t=>t.Count).ToList();
                Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
                File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), tokens.Select(t=>string.Format("{0} {1}", t.Token, t.Count)));
            }
            else if (args[0].ToLower() == "about")
            {
                var about = File.ReadAllText(@"..\..\..\readme.md");
                var dir = Path.Combine(Environment.CurrentDirectory, "about");
                using (var writer = new IndexWriter(dir, new Analyzer()))
                {
                    writer.Write(new Document
                    {
                        Fields = new Dictionary<string, List<string>>
                        {
                            {"body", new List<string> {about}}
                        }
                    });
                }
                var scanner = new Scanner(dir);
                var timer = new Stopwatch();
                timer.Start();
                var tokens = scanner.GetAllTokens("body").OrderByDescending(t => t.Count).ToList();
                Console.WriteLine("Tokens fetched from disk in {0} ms. Writing...\r\n", timer.ElapsedMilliseconds);
                File.WriteAllLines(Path.Combine(dir, "_about.txt"), tokens.Select(t => string.Format("{0} {1}", t.Token, t.Count)));
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
    }
}
