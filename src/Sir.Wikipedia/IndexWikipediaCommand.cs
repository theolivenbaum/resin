using Microsoft.Extensions.Logging;
using Sir.Search;
using Sir.VectorSpace;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
            var skip = args.ContainsKey("skip") ? int.Parse(args["skip"]) : 0;
            var take = args.ContainsKey("take") ? int.Parse(args["take"]) : int.MaxValue;
            var pageSize = int.Parse(args["pageSize"]);
            var reportSize = args.ContainsKey("reportSize") ? int.Parse(args["reportSize"]) : 1000;

            var collectionId = collection.ToHash();
            var fieldsToStore = new HashSet<string> { "language", "wikibase_item", "title", "text" };
            var fieldsToIndex = new HashSet<string> { "language", "title", "text" };

            if (take == 0)
                take = int.MaxValue;

            var debugger = new IndexDebugger();

            using (var sessionFactory = new SessionFactory(dataDirectory, logger))
            {
                sessionFactory.Truncate(collectionId);

                using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
                {
                    var payload = WikipediaHelper.ReadWP(fileName, skip, take, fieldsToStore, fieldsToIndex);

                    IDictionary<long, VectorNode> index;

                    foreach (var page in payload.Batch(pageSize))
                    {
                        using (var indexSession = sessionFactory.CreateIndexSession(new BagOfCharsModel()))
                        {
                            foreach (var batch in page.Batch(reportSize))
                            {
                                var time = Stopwatch.StartNew();

                                foreach (var document in batch)
                                {
                                    var documentId = writeSession.Put(document);

                                    foreach (var field in document.Fields)
                                    {
                                        if (field.Value != null && field.Index)
                                        {
                                            indexSession.Put(documentId, field.Id, field.Value.ToString());
                                        }
                                    }
                                }

                                var debugInfo = debugger.GetDebugInfo(indexSession);

                                if (debugInfo != null)
                                {
                                    logger.LogInformation(debugInfo);
                                }
                            }

                            index = indexSession.InMemoryIndex;

                            using (var stream = new IndexFileStreamProvider(collectionId, sessionFactory, logger:logger))
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
        }

        private static void Print(string name, VectorNode tree)
        {
            var diagram = PathFinder.Visualize(tree);
            File.WriteAllText($@"c:\temp\{name}.txt", diagram);
        }
    }
}