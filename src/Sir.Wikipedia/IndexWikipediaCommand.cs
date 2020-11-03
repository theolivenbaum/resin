using Microsoft.Extensions.Logging;
using Sir.Search;
using Sir.VectorSpace;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Wikipedia
{
    /// <summary>
    /// Download JSON search index dump here: 
    /// https://dumps.wikimedia.org/other/cirrussearch/current/enwiki-20201026-cirrussearch-content.json.gz
    /// </summary>
    public class IndexWikipediaCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var fileName = args["fileName"];
            var collection = args["collection"];
            var skip = int.Parse(args["skip"]);
            var take = args.ContainsKey("take") ? int.Parse(args["take"]) : int.MaxValue;
            var pageSize = int.Parse(args["pageSize"]);
            var reportSize = args.ContainsKey("reportSize") ? int.Parse(args["reportSize"]) : 1000;

            var collectionId = collection.ToHash();
            var fieldsToStore = new HashSet<string> { "language", "url", "title", "description" };
            var fieldsToIndex = new HashSet<string> { "language", "title", "description" };

            if (take == 0)
                take = int.MaxValue;

            var debugger = new IndexDebugger();
            var payload = WikipediaHelper.ReadWP(fileName, skip, take)
                .Select(x => new Dictionary<string, object>
                        {
                                            { "language", x["language"].ToString() },
                                            { "url", string.Format("www.wikipedia.org/search-redirect.php?family=wikipedia&language={0}&search={1}", x["language"], x["title"]) },
                                            { "title", x["title"] },
                                            { "description", x["text"] }
                        });

            using (var sessionFactory = new SessionFactory(dataDirectory, logger))
            {
                sessionFactory.Truncate(collectionId);

                IDictionary<long, VectorNode> index;

                foreach (var batch in payload.Batch(pageSize))
                {
                    using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
                    using (var indexSession = sessionFactory.CreateIndexSession(new BagOfCharsModel()))
                    {
                        foreach (var reportBatch in batch.Batch(reportSize))
                        {
                            var time = Stopwatch.StartNew();

                            foreach (var document in reportBatch)
                            {
                                var documentId = writeSession.Put(document, fieldsToStore);

                                foreach (var kv in document)
                                {
                                    if (fieldsToIndex.Contains(kv.Key) && kv.Value != null)
                                    {
                                        var keyId = writeSession.EnsureKeyExists(kv.Key);

                                        indexSession.Put(documentId, keyId, kv.Value.ToString());
                                    }
                                }
                            }

                            var debugInfo = debugger.GetDebugInfo(indexSession);

                            if (debugInfo != null)
                            {
                                logger.LogInformation(debugInfo);
                            }
                        }

                        index = indexSession.GetInMemoryIndex();

                        using (var stream = new IndexFileStreamProvider(collectionId, sessionFactory, logger))
                        {
                            stream.Write(index);
                        }

                        foreach (var column in index)
                        {
                            Print($"wikipedia.{column.Key}", column.Value);
                        }
                    }
                }
            }
        }

        private static void Print(string name, VectorNode tree)
        {
            var diagram = PathFinder.Visualize(tree);
            File.WriteAllText($@"c:\temp\{name}.txt", diagram);
        }
    }
}