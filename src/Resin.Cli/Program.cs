using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Resin.Analysis;
using Resin.Querying;
using System.Linq;
using System.Reflection;
using log4net.Config;
using log4net;
using Resin.IO;

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
            var assembly = Assembly.GetEntryAssembly();
            var logRepository = LogManager.GetRepository(assembly);
            var currentDir = Path.GetDirectoryName(assembly.Location);
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(currentDir, "log4net.config")));
            
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

        static void Delete(string[] args)
        {
            var ids = args[Array.IndexOf(args, "--ids") + 1].Split(',');
            string dir = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];

            var timer = new Stopwatch();
            timer.Start();

            new DeleteByPrimaryKeyOperation(dir, ids).Commit();

            Console.WriteLine("delete operation took {0}", timer.Elapsed);
        }

        static void Query(string[] args)
        {
            string dir = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];


            var q = args[Array.IndexOf(args, "-q") + 1];
            var page = 0;
            var size = 10;

            Result result;

            if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);

            var timer = new Stopwatch();
            timer.Start();

            using (var s = new Searcher(dir))
            {
                result = s.Search(q, page, size);

                timer.Stop();

                var docs = result.Docs;

                PrintHeaders(docs[0].Document.Fields.Select(f=>f.Key).ToArray());

                var highlight = new QueryParser(new Analyzer()).Parse(q).ToList().GroupBy(y => y.Field).ToDictionary(g => g.Key, g => g.First().Value);

                foreach (var doc in docs)
                {
                    Print(doc, highlight);
                }

                Console.WriteLine("\r\n{0}-{1} results of {2} in {3}", page * size, docs.Count + (page * size), result.Total, timer.Elapsed);
            }

        }

        private static void PrintHeaders(IEnumerable<string> labels)
        {
            Console.WriteLine();

            Console.Write("score\t");

            Console.WriteLine(string.Join("\t", labels));

            Console.WriteLine();
        }

        private static void Print(ScoredDocument doc, IDictionary<string, string> highlight)
        {
            Console.Write(doc.Score.ToString("#.##") + "\t");

            foreach(var field in doc.Document.Fields.Values)
            {
                Print(field.Value, highlight.ContainsKey(field.Key) ? highlight[field.Key] : null);
            }
            Console.WriteLine();
        }

        private static void Print(string value, string highlight)
        {
            var body = value;

            if (highlight != null)
            {
                var ix = body.IndexOf(highlight, StringComparison.CurrentCultureIgnoreCase);

                if (ix > 0)
                {
                    body = body.Substring(ix, body.Length - ix);
                }
            }

            Console.Write(body.Substring(0, Math.Min(80, body.Length)) + "\t");
        }

        static void Write(string[] args)
        {
            var take = int.MaxValue;
            var skip = 0;
            bool gzip = false;
            bool lz = false;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);
            if (Array.IndexOf(args, "--gzip") > 0) gzip = true;
            if (Array.IndexOf(args, "--lz") > 0) lz = true;

            var compression = gzip ? Compression.GZip : lz ? Compression.Lz : Compression.NoCompression;

            var fileName = args[Array.IndexOf(args, "--file") + 1];
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            Console.WriteLine("writing...");

            var docs = new List<Dictionary<string, string>>();

            var writeTimer = new Stopwatch();
            writeTimer.Start();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var writer = new CliLineDocUpsertOperation(dir, new Analyzer(), skip, take, compression, null, fileName))
            {
                writer.Commit();
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }
    }
}
