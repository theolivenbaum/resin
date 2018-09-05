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
        static void Main(string[] args)
        {
            var command = args[0].ToLower();

            if (command == "reindex")
            {
                Reindex(args[1], int.Parse(args[2]));
            }
            Console.WriteLine("done");
            Console.Read();
        }

        private static void Reindex(string dir, int batchSize)
        {
            var timer = new Stopwatch();
            var batchTimer = new Stopwatch();
            timer.Start();

            var files = Directory.GetFiles(dir, "*.docs");

            Console.WriteLine("re-indexing process found {0} document files", files.Length);

            foreach (var docFileName in files)
            {
                var name = Path.GetFileNameWithoutExtension(docFileName)
                    .Split(".", StringSplitOptions.RemoveEmptyEntries);

                var collectionId = ulong.Parse(name[0]);

                using (var readSession = new DocumentReadSession(collectionId, new LocalStorageSessionFactory(dir, new LatinTokenizer())))
                {
                    foreach (var batch in readSession.ReadDocs().Batch(batchSize))
                    {
                        batchTimer.Restart();

                        using (var writeSession = new LocalStorageSessionFactory(dir, new LatinTokenizer()).CreateWriteSession(collectionId))
                        {
                            var job = new IndexJob(collectionId, batch);

                            writeSession.WriteToInMemoryIndex(job);
                        }
                        Console.WriteLine("wrote batch to {0} in {1}", collectionId, batchTimer.Elapsed);
                    }
                }
            }
            Console.WriteLine("rebuilt {0} indexes in {1}", files.Length, timer.Elapsed);
        }
    }
}
