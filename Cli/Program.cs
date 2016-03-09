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
            var count = 0;
            foreach (var line in File.ReadLines(@"d:\wikipedia\wikipedia.json"))
            {
                File.AppendAllText(@"d:\0.json", line + "\r\n");
                count++;
                if (count > 10) break;
            }
            if (args[0].ToLower() == "read")
            {
                var fileName = args[Array.IndexOf(args, "--file") + 1];
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                var json = File.ReadAllText(fileName);
                var docs = JsonConvert.DeserializeObject<List<Document>>(json);
                Console.WriteLine("Read {0}. Found {1} docs.", fileName, docs.Count);
                Console.Write("Wrote: ");
                var cursorPos = Console.CursorLeft;
                var done = 0;
                var timer = new Stopwatch();
                timer.Start();
                using (var w = new IndexWriter(dir, new Analyzer()))
                {
                    foreach (var doc in docs)
                    {
                        w.Write(doc);
                        Console.SetCursorPosition(cursorPos, Console.CursorTop);
                        Console.Write(++done);
                    }    
                }
                Console.WriteLine("");
                Console.WriteLine("Index created in " + timer.Elapsed);
            }
            else if (args[0].ToLower() == "query")
            {
                var timer = new Stopwatch();
                timer.Start();
                var dir = args[Array.IndexOf(args, "--dir") + 1];
                if(Directory.Exists(dir)) Directory.Delete(dir, true);
                var scanner = new DocumentScanner(dir);
                var reader = new IndexReader(scanner);
                var q = args[Array.IndexOf(args, "-q") + 1].Split(':');
                var docs = reader.GetDocuments(q[0], q[1]).ToList();
                foreach (var doc in docs)
                {
                    Console.WriteLine(string.Join(",", doc["title"]));
                }
                Console.WriteLine("{0} results in {1}", docs.Count, timer.Elapsed);
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("rn.exe read --file source.json --dir c:\\target_dir");
                Console.WriteLine("rn.exe query --dir c:\\my_index -q field:value");
            }
        }
    }
}
