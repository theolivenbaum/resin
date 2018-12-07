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
        private static StreamWriter _log;

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("processing command: {0}", string.Join(" ", args));

                _log = Logging.CreateWriter("dbutil");

                Logging.SendToConsole = true;

                var command = args[0].ToLower();

                if (command == "index" && args.Length == 6)
                {
                    // example: index C:\projects\resin\src\Sir.HttpServer\App_Data www 0 10000 1000

                    Index(
                        dir: args[1],
                        collection: args[2],
                        skip: int.Parse(args[3]),
                        take: int.Parse(args[4]),
                        batchSize: int.Parse(args[5]));
                }
                else if (command == "query" && args.Length == 3)
                {
                    // example: query C:\projects\resin\src\Sir.HttpServer\App_Data www

                    Task.Run(() => Query(dir: args[1], collectionId: args[2])).Wait();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.Read();
            }
        }

        private static async Task Query(string dir, string collectionId)
        {
            var tokenizer = new LatinTokenizer();
            var qp = new KeyValueBooleanQueryParser();
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

                using (var session = sessionFactory.CreateReadSession(collectionId))
                {
                    var result = await session.Read(q);
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

        private static void Index(string dir, string collection, int skip, int take, int batchSize)
        {
            var timer = new Stopwatch();
            timer.Start();

            var files = Directory.GetFiles(dir, "*.docs");
            var batchNo = 0;

            using (var sessionFactory = new SessionFactory(dir, new LatinTokenizer(), new IniConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "sir.ini"))))
            foreach (var docFileName in files)
            {
                var name = Path.GetFileNameWithoutExtension(docFileName)
                    .Split(".", StringSplitOptions.RemoveEmptyEntries);

                var collectionId = name[0];

                if (collectionId == collection.ToHash().ToString())
                {
                    using (var readSession = new DocumentStreamSession(collection, sessionFactory))
                    {
                        var docs = readSession.ReadDocs();

                        if (skip > 0)
                        {
                            docs = docs.Skip(skip);
                        }

                        if (take > 0)
                        {
                            docs = docs.Take(take);
                        }

                        var writeTimer = new Stopwatch();

                        foreach (var batch in docs.Batch(batchSize))
                        {
                            writeTimer.Restart();

                            var job = new IndexingJob(batch);

                            using (var indexSession = sessionFactory.CreateIndexSession(collection))
                            {
                                indexSession.Write(job);
                            }

                            _log.Log(string.Format("batch {0} done in {1}", batchNo++, writeTimer.Elapsed));
                        }
                    }
                    break;
                }
            }

            _log.Log(string.Format("indexing took {0}", timer.Elapsed));
        }
    }
}
