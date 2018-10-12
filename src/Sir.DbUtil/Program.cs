using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Sir.Store;

namespace Sir.DbUtil
{
    class Program
    {
        private static StreamWriter _log;

        static void Main(string[] args)
        {
            Console.WriteLine("processing command: {0}", string.Join(" ", args));

            _log = Logging.CreateWriter("dbutil");

            Logging.SendToConsole = true;

            var command = args[0].ToLower();

            if (command == "build" && args.Length == 6)
            {
                // example: build C:\projects\resin\src\Sir.HttpServer\App_Data www 0 10000 1000

                BuildIndex(
                    dir: args[1], 
                    collection: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]), 
                    batchSize: int.Parse(args[5]));
            }
            else if (command == "query" && args.Length == 3)
            {
                // example: query C:\projects\resin\src\Sir.HttpServer\App_Data www

                Query(dir: args[1], collection: args[2]);
            }
        }

        private static void Query(string dir, string collection)
        {
            var tokenizer = new LatinTokenizer();
            var qp = new BooleanKeyValueQueryParser();
            var sessionFactory = new LocalStorageSessionFactory(dir, tokenizer);

            while (true)
            {
                Console.Write("query>");

                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input == "q" || input == "quit")
                {
                    break;
                }

                var q = qp.Parse(input, tokenizer);

                using (var session = sessionFactory.CreateReadSession(collection.ToHash()))
                {
                    long total;
                    var docs = session.Read(q, out total);

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

        private static void BuildIndex(string dir, string collection, int skip, int take, int batchSize)
        {
            var timer = new Stopwatch();
            timer.Start();

            var files = Directory.GetFiles(dir, "*.docs");
            var sessionFactory = new LocalStorageSessionFactory(dir, new LatinTokenizer());
            var colId = collection.ToHash();
            var batchNo = 0;

            foreach (var docFileName in files)
            {
                var name = Path.GetFileNameWithoutExtension(docFileName)
                    .Split(".", StringSplitOptions.RemoveEmptyEntries);

                var collectionId = ulong.Parse(name[0]);

                if (collectionId == colId)
                {
                    using (var readSession = new DocumentReadSession(collectionId, sessionFactory))
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

                            var job = new AnalyzeJob(collectionId, batch);

                            using (var indexSession = sessionFactory.CreateIndexSession(collectionId))
                            {
                                indexSession.Write(job);
                            }

                            _log.Log(string.Format("batch {0} done in {1}", batchNo++, writeTimer.Elapsed));

                            sessionFactory.LoadIndex();
                        }
                    }
                    break;
                }
            }

            _log.Log(string.Format("writing index took {0}", timer.Elapsed));
        }
    }
}
