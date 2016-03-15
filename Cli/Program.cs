using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                Console.WriteLine("Analyzing");
                var scanner = new Scanner(dir);
                File.WriteAllLines(Path.Combine(dir, "_label.txt"), scanner.GetAllTerms("label"));
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

                using (var s = new Searcher(dir))
                {
                    for (int i = 0; i < 1; i++)
                    {
                        s.Search("label:definetly_not_in_the_lexicon").ToList(); // warm up the "label" field
                    }
                    timer.Start();
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
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe write --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
            }
        }
    }
}
