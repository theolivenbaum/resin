using System;
using System.Diagnostics;
using System.IO;
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
                // example: index C:\projects\resin\src\Sir.HttpServer\App_Data www 0 10000 1000 true

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

                Query(dir: args[1], collectionName: args[2]);
            }
            else if (command == "create-bow")
            {
                // example: create-bow C:\projects\resin\src\Sir.HttpServer\App_Data www

                CreateBOWModel(dir: args[1], collectionName: args[2], skip: int.Parse(args[3]), take: int.Parse(args[4]));
            }
            else if (command == "validate")
            {
                // example: validate C:\projects\resin\src\Sir.HttpServer\App_Data www 0 3000

                Validate(dir: args[1], collectionName: args[2], skip: int.Parse(args[3]), take: int.Parse(args[4]));
            }
            else if (command == "optimize")
            {
                // example: optimize C:\projects\resin\src\Sir.HttpServer\App_Data www

                await Optimize(dir: args[1], collectionName: args[2]);
            }

            Console.Read();
        }

        private static async Task Optimize(string dir, string collectionName)
        {
            var files = Directory.GetFiles(dir, "*.docs");
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration("sir.ini")))
            {
                using (var optimizeSession = sessionFactory.CreateOptimizeSession(collectionName, collectionName.ToHash()))
                {
                    await optimizeSession.Optimize();
                }
            }

            Logging.Log(null, string.Format("{0} optimize operation took {1}", collectionName, time.Elapsed));
        }

        private static void Query(string dir, string collectionName)
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

                var q = qp.Parse(input, tokenizer);
                q.Skip = 0;
                q.Take = 100;

                using (var session = sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                {
                    var result = session.Read(q);
                    var docs = result.Docs;

                    if (docs.Count > 0)
                    {
                        var index = 0;

                        foreach (var doc in docs.Take(10))
                        {
                            Console.WriteLine("{0} {1} {2}", index++, doc["__score"], doc["title"]);
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
                                        indexSession.EmbedTerms(doc);
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
