using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;
using Sir.Store;

namespace Sir.DbUtil
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("processing command: {0}", string.Join(" ", args));

            Logging.SendToConsole = true;

            var command = args[0].ToLower();

            if (command == "index")
            {
                // example: index C:\projects\resin\src\Sir.HttpServer\App_Data www 0 10000 1000

                Index(
                    dir: args[1],
                    collectionName: args[2],
                    skip: int.Parse(args[3]),
                    take: int.Parse(args[4]),
                    batchSize: int.Parse(args[5]));
            }
            else if (command == "query")
            {
                // example: query C:\projects\resin\src\Sir.HttpServer\App_Data www

                await Query(
                    dir: args[1], 
                    collectionName: args[2]);
            }
            else if (command == "create-bow")
            {
                // example: create-bow C:\projects\resin\src\Sir.HttpServer\App_Data www 0 1000

                CreateBOWModel(
                    dir: args[1], 
                    collectionName: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]));
            }
            else if (command == "validate")
            {
                // example: validate C:\projects\resin\src\Sir.HttpServer\App_Data www 0 3000

                Validate(
                    dir: args[1], 
                    collectionName: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]));
            }
            else if (command == "warmup")
            {
                // example: warmup C:\projects\resin\src\Sir.HttpServer\App_Data https://didyougogo.com www 0 3000

                var dir = args[1];
                var uri = new Uri(args[2]);
                var collection = args[3];
                var skip = int.Parse(args[4]);
                var take = int.Parse(args[5]);

                Warmup(
                    dir, 
                    uri, 
                    collection, 
                    skip, 
                    take);
            }
            else if (command == "optimize")
            {
                // example: optimize C:\projects\resin\src\Sir.HttpServer\App_Data www

                Optimize(
                    dir: args[1], 
                    collectionName: args[2]);
            }
            else if (command == "mmf")
            {
                // example: mmf C:\projects\resin\src\Sir.HttpServer\App_Data\6604389855880847730.3 C:\projects\resin\src\Sir.HttpServer\App_Data\6604389855880847730.4

                MMF(args.Skip(1).ToArray());
            }
            else
            {
                Console.WriteLine("unknown command: {0}", command);
            }

            Console.WriteLine("press any key to exit");
            Console.Read();
        }

        private static void MMF(params string[] files)
        {
            var dir = Path.GetDirectoryName(files[0]);
            var mapped = new List<MemoryMappedFile>();

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            foreach (var file in files)
            {
                mapped.Add(sessionFactory.CreateMMF(file));
            }

            Console.WriteLine("mapping complete.");
            Console.Read();

            foreach(var mmf in mapped)
            {
                mmf.Dispose();
            }
        }

        private static void Warmup(string dir, Uri uri, string collectionName, int skip, int take)
        {
            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                {
                    using (var session = sessionFactory.CreateWarmupSession(collectionName, collectionName.ToHash(), uri.ToString()))
                    {
                        session.Warmup(documentStreamSession.ReadDocs(skip, take), 0, 1, 2, 3, 6);
                    }
                }
            }
        }

        private static void Optimize(string dir, string collectionName)
        {
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var optimizeSession = sessionFactory.CreateOptimizeSession(collectionName, collectionName.ToHash()))
                {
                    optimizeSession.Optimize();
                }
            }

            Logging.Log(null, string.Format("{0} optimize operation took {1}", collectionName, time.Elapsed));
        }

        private static async Task Query(string dir, string collectionName)
        {
            var tokenizer = new LatinTokenizer();
            var qp = new TermQueryParser();
            var sessionFactory = new SessionFactory(
                dir,
                tokenizer,
                new IniConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "sir.ini")));

            while (true)
            {
                Console.Write("query>");

                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input == "q" || input == "quit")
                {
                    break;
                }

                var q = qp.Parse(collectionName.ToHash(), input, tokenizer);
                q.Skip = 0;
                q.Take = 100;

                using (var session = sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                {
                    var result = await session.Read(q);
                    var docs = result.Docs;

                    if (docs.Count > 0)
                    {
                        var index = 0;

                        foreach (var doc in docs.Take(10))
                        {
                            Console.WriteLine("{0} {1} {2}", index++, doc["___score"], doc["title"]);
                        }
                    }
                }
            }
        }

        private static void Index(string dir, string collectionName, int skip, int take, int batchSize)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var fullTime = Stopwatch.StartNew();
            var batchCount = 0;

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                foreach (var docFileName in files)
                {
                    var name = Path.GetFileNameWithoutExtension(docFileName)
                        .Split(".", StringSplitOptions.RemoveEmptyEntries);

                    var collectionId = ulong.Parse(name[0]);

                    if (collectionId == collectionName.ToHash())
                    {
                        using (var readSession = sessionFactory.CreateDocumentStreamSession(name[0], collectionId))
                        {
                            var docs = readSession.ReadDocs(skip, take);

                            foreach (var batch in docs.Batch(batchSize))
                            {
                                var timer = Stopwatch.StartNew();

                                using (var indexSession = sessionFactory.CreateIndexSession(collectionName, collectionId))
                                {
                                    foreach (var doc in batch)
                                    {
                                        indexSession.Index(doc, 0, 1, 2, 3, 6);
                                    }
                                }

                                Logging.Log(null, string.Format("indexed batch #{0} in {1}", batchCount++, timer.Elapsed));
                            }
                        }

                        break;
                    }
                }
            }

            Logging.Log(null, string.Format("indexed {0} batches in {1}", batchCount, fullTime.Elapsed));
        }

        private static void CreateBOWModel(string dir, string collectionName, int skip, int take)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var bowSession = sessionFactory.CreateBOWSession(collectionName, collectionName.ToHash()))
                {
                    bowSession.Write(documentStreamSession.ReadDocs(skip, take), 0, 1, 2, 3, 6);
                }
            }

            Logging.Log(null, string.Format("{0} BOW operation took {1}", collectionName, time.Elapsed));
        }

        private static void Validate(string dir, string collectionName, int skip, int take)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var validateSession = sessionFactory.CreateValidateSession(collectionName, collectionName.ToHash()))
                {
                    validateSession.Validate(documentStreamSession.ReadDocs(skip, take), 0, 1, 2, 3, 6);
                }
            }

            Logging.Log(null, string.Format("{0} validate operation took {1}", collectionName, time.Elapsed));
        }
    }
}
