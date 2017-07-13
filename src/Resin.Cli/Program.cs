using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Resin.Analysis;
using System.Linq;
using System.Reflection;
using log4net.Config;
using log4net;
using Resin.IO;
using System.Text;
using Newtonsoft.Json;
using DocumentTable;

namespace Resin.Cli
{
    class Program
    {
        // query --dir c:\temp\resin_data\mystore -q "label:the" -p 0 -s 10
        // write --file c:\temp\0wikipedia.json --dir c:\temp\resin_data\mystore --pk "label" --skip 0 --take 10000 --lz --gzip
        // delete --ids "Q1476435" --dir c:\temp\resin_data\mystore
        // merge --dir c:\temp\resin_data\mystore
        // rewrite --file c:\temp\resin_data\636326999602241674.rdoc --dir c:\temp\resin_data\pg --pk "url"
        // export --source-file c:\temp\resin_data\636326999602241674.rdoc --target-file c:\temp\636326999602241674.rdoc.json

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
            else if (args[0].ToLower() == "merge")
            {
                Merge(args);
            }
            else if (args[0].ToLower() == "delete")
            {
                Delete(args);
            }
            else if (args[0].ToLower() == "rewrite")
            {
                if (Array.IndexOf(args, "--file") == -1)
                {
                    Console.WriteLine("I need a file.");
                    return;
                }
                Rewrite(args);
            }
            else if (args[0].ToLower() == "export")
            {
                if (Array.IndexOf(args, "--source-file") == -1)
                {
                    Console.WriteLine("I need a file.");
                    return;
                }
                Export(args);
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine(@"
	rn write --file source_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]
	rn query --dir store_directory -q query_statement [-p page_number] [-s page_size]
	rn delete --ids comma_separated_list_of_ids --dir store_directory
	rn merge --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take]
    rn rewrite --file rdoc_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]
    rn export --source-file rdoc_filename --target-file json_filename
");
            }
        }

        static void Merge(string[] args)
        {
            string dir = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];

            if (dir != null)
            {
                using (var merge = new MergeCommand(dir))
                {
                    var result = merge.Commit();
                    
                    if (result == -1)
                    {
                        Console.Write("nothing to merge or truncate in dir {0}", dir);
                    }
                }
            }
        }

        static void Delete(string[] args)
        {
            var ids = args[Array.IndexOf(args, "--ids") + 1].Split(',');
            string dir = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];

            var timer = new Stopwatch();
            timer.Start();

            new DeleteByPrimaryKeyCommand(dir, ids).Commit();

            Console.WriteLine("delete operation took {0}", timer.Elapsed);
        }

        static void Query(string[] args)
        {
            string dir = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];

            var q = args[Array.IndexOf(args, "-q") + 1];
            var page = 0;
            var size = 10;

            ScoredResult result;

            if (Array.IndexOf(args, "-p") > 0) page = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-s") > 0) size = int.Parse(args[Array.IndexOf(args, "-s") + 1]);

            var timer = new Stopwatch();
            timer.Start();

            using (var s = new Searcher(dir))
            {
                result = s.Search(q, page, size);

                timer.Stop();

                var docs = result.Docs;

                if (docs.Any())
                {
                    foreach (var doc in docs)
                    {
                        Print(doc);
                    }
                }
                

                Console.WriteLine("\r\n{0}-{1} results of {2} in {3}", (page * size)+1, docs.Count + (page * size), result.Total, timer.Elapsed);
            }

        }

        private static void PrintHeaders(IEnumerable<string> labels)
        {
            Console.WriteLine();

            Console.Write("score\t");

            Console.WriteLine(string.Join("\t", labels));

            Console.WriteLine();
        }

        private static void Print(ScoredDocument doc)
        {
            Console.Write(doc.Score.ToString("#.##") + "\t");
            Console.WriteLine(doc.Document.Fields["title"].Value);
        }

        private static void Print(string value)
        {
            Console.Write(value.Substring(0, Math.Min(40, value.Length)) + "\t");
        }

        static void Write(string[] args)
        {
            var take = int.MaxValue;
            var skip = 0;
            bool gzip = false;
            bool lz = false;
            string pk = null;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);
            if (Array.IndexOf(args, "--gzip") > 0) gzip = true;
            if (Array.IndexOf(args, "--lz") > 0) lz = true;
            if (Array.IndexOf(args, "--pk") > 0) pk = args[Array.IndexOf(args, "--pk") + 1];

            var compression = gzip ? Compression.GZip : lz ? Compression.Lz : Compression.NoCompression;

            var fileName = args[Array.IndexOf(args, "--file") + 1];
            string dir = null;
            string indexName = null;

            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];
            if (Array.IndexOf(args, "--name") > 0) indexName = args[Array.IndexOf(args, "--name") + 1];

            var docs = new List<Dictionary<string, string>>();

            var writeTimer = new Stopwatch();
            writeTimer.Start();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var documents = new JsonDocumentStream(fileName, skip, take, pk))
            using (var upsert = new UpsertCommand(dir, new Analyzer(), compression, documents))
            {
                upsert.Write();
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }

        static void Rewrite(string[] args)
        {
            var take = int.MaxValue;
            var skip = 0;
            string pk = null;
            bool gzip = false;
            bool lz = false;
            string dir = null;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);
            if (Array.IndexOf(args, "--pk") > 0) pk = args[Array.IndexOf(args, "--pk") + 1];
            if (Array.IndexOf(args, "--gzip") > 0) gzip = true;
            if (Array.IndexOf(args, "--lz") > 0) lz = true;
            if (Array.IndexOf(args, "--dir") > 0) dir = args[Array.IndexOf(args, "--dir") + 1];

            var compression = gzip ? Compression.GZip : lz ? Compression.Lz : Compression.NoCompression;
            var dataFileName = args[Array.IndexOf(args, "--file") + 1];
            var ixFileName = Directory.GetFiles(Path.GetDirectoryName(dataFileName), "*.ix")
                .OrderBy(s => s).First();
            var ix = BatchInfo.Load(ixFileName);

            Console.WriteLine("rewriting...");

            var writeTimer = new Stopwatch();
            writeTimer.Start();

            using (var stream = new FileStream(dataFileName, FileMode.Open))
            using (var documents = new DtblStream(stream, ix, skip, take))
            using (var upsert = new UpsertCommand(dir, new Analyzer(), compression, documents))
            {
                upsert.Write();
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }

        static void Export(string[] args)
        {
            var take = int.MaxValue;
            var skip = 0;

            if (Array.IndexOf(args, "--take") > 0) take = int.Parse(args[Array.IndexOf(args, "--take") + 1]);
            if (Array.IndexOf(args, "--skip") > 0) skip = int.Parse(args[Array.IndexOf(args, "--skip") + 1]);

            var sourceFileName= args[Array.IndexOf(args, "--source-file") + 1];
            var targetFileName = args[Array.IndexOf(args, "--target-file") + 1];

            var dir = Path.GetDirectoryName(sourceFileName);
            var version = Path.GetFileNameWithoutExtension(sourceFileName);
            var ix = BatchInfo.Load(Path.Combine(dir, version + ".ix"));

            Console.WriteLine("migrating...");

            var writeTimer = new Stopwatch();
            writeTimer.Start();

            using (var sourceStream = new FileStream(sourceFileName, FileMode.Open))
            using (var targetStream = new FileStream(targetFileName, FileMode.Create))
            using (var jsonWriter = new StreamWriter(targetStream, Encoding.UTF8))
            using (var documents = new DtblStream(sourceStream, ix, skip, take))
            {
                jsonWriter.WriteLine("[");

                foreach (var document in documents.ReadSource())
                {
                    var dic = document.Fields.ToDictionary(x => x.Key, y => y.Value.Value);
                    var json = JsonConvert.SerializeObject(dic, Formatting.None);
                    jsonWriter.WriteLine(json);
                }

                jsonWriter.Write("]");
            }

            Console.WriteLine("write operation took {0}", writeTimer.Elapsed);
        }
    }
}
