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
                var docs = JsonConvert.DeserializeObject<List<Document>>(json);
                var batches = docs.Skip(skip).Take(take).Chunkify(1000);
                Console.WriteLine("Read {0}. Found {1} docs.", fileName, docs.Count);
                Console.Write("Writing: ");
                var cursorPos = Console.CursorLeft;
                var done = 0;
                var timer = new Stopwatch();
                foreach (var batch in batches)
                {
                    var ds = batch.ToList();
                    timer.Start();
                    using (var w = new IndexWriter(dir, new Analyzer(), overwrite:true))
                    {
                        foreach (var d in ds)
                        {
                            w.Write(d);
                            Console.SetCursorPosition(cursorPos, Console.CursorTop);
                            Console.Write(++done);
                        }
                    }
                    timer.Stop();
                }
                
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
                var timer = new Stopwatch();
                timer.Start();
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var scanner = new DocumentScanner(dir);
                var reader = new IndexReader(scanner);
                var q = args[Array.IndexOf(args, "-q") + 1];
                var terms = new QueryParser(new Analyzer()).Parse(q).ToList();
                var docs = reader.GetDocuments(terms).ToList(); 
                var position = 0;
                foreach (var doc in docs)
                {
                    Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
                }
                Console.WriteLine("{0} results in {1}", docs.Count, timer.Elapsed);
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
