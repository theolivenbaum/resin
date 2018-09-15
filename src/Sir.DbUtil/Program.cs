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
            Console.WriteLine("parsing command: {0}", args);

            _log = Logging.CreateWriter("dbutil");

            Logging.SendToConsole = true;

            var command = args[0].ToLower();

            if (command == "load")
            {
                Load(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]));
            }
            Console.WriteLine("done");
            Console.Read();
        }

        private static void Load(string dir, string collection, int skip, int take, int batchSize)
        {
            var timer = new Stopwatch();
            timer.Start();

            var files = Directory.GetFiles(dir, "*.docs");
            var sessionFactory = new LocalStorageSessionFactory(dir, new LatinTokenizer());
            var colId = collection.ToHash();

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

                            _log.Log(string.Format("indexed {0} docs in {1}", batchSize, writeTimer.Elapsed));

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
