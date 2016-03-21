using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace Resin
{
    // write --file e:\wikipedia\wikipedia_resin.json --dir c:\temp\resin\0 --skip 0 --take 10000
    // query --dir c:\temp\resin\0 -q "label:roman"
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
                using (var w = new IndexWriter(dir, new Analyzer()))
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

                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var q = args[Array.IndexOf(args, "-q") + 1];

                var timer = new Stopwatch();
                timer.Start();
                using (var s = new Searcher(dir))
                {
                    Console.WriteLine("Searcher initialized in {0} ms", timer.ElapsedMilliseconds);

                    timer.Restart();
                    for (int i = 0; i < 1; i++)
                    {
                        s.Search(q).ToList(); // warm up the "label" field
                    }
                    Console.WriteLine("Warm-up in {0} ms\r\n", timer.ElapsedMilliseconds);

                    timer.Restart();
                    var docs = s.Search(q).ToList();
                    var elapsed = timer.Elapsed.TotalMilliseconds;
                    
                    var position = 0;
                    foreach (var doc in docs)
                    {
                        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
                    }
                    Console.WriteLine("{0} results in {1} ms", docs.Count, elapsed);                  
                }
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
            else if (args[0].ToLower() == "trie")
            {
                if (Array.IndexOf(args, "--dir") == -1)
                {
                    Console.WriteLine("I need a directory.");
                    return;
                }
                const string q = "ring";
                const string field = "label";
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var timer = new Stopwatch();
                var scanner = new Scanner(dir);

                timer.Restart();
                var tokens = scanner.GetAllTokens(field).Select(t=>t.Token).ToList();
                Console.WriteLine("Tokens fetched from disk in {0} ms.\r\n", timer.ElapsedMilliseconds);
                
                timer.Restart();
                var trie = new Trie(tokens);
                Console.WriteLine("Trie built in {0} ms\r\n", timer.ElapsedMilliseconds);
                
                timer.Restart();
                var words = trie.WordsStartingWith(q).ToList();
                Console.WriteLine("Trie scan found {0} words in {1} ms. The first 20:\r\n", words.Count, timer.ElapsedMilliseconds);
                foreach (var w in words.Take(20)) Console.WriteLine(w);
                var fileName = Path.Combine(dir, "field.tri");
                
                timer.Restart();
                trie.Save(fileName);
                Console.WriteLine("Trie serialized in {0} ms\r\n", timer.ElapsedMilliseconds);
                
                timer.Restart();
                trie = Trie.Load(fileName);
                Console.WriteLine("Trie deserialized in {0} ms\r\n", timer.ElapsedMilliseconds);
                
                timer.Restart();
                words = trie.WordsStartingWith(q).ToList();
                Console.WriteLine("Trie scan found {0} words in {1} ms. The first 20:\r\n", words.Count, timer.ElapsedMilliseconds);
                foreach (var w in words.Take(20)) Console.WriteLine(w);
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe write --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
            }
        }
    }
}
