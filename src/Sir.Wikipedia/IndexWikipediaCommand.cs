using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using Sir.VectorSpace;
using System.Collections.Generic;
using System.IO;

namespace Sir.Wikipedia
{
    /// <summary>
    /// Download JSON search index dump here: 
    /// https://dumps.wikimedia.org/other/cirrussearch/current/enwiki-20201026-cirrussearch-content.json.gz
    /// </summary>
    /// <example>
    /// indexwikipedia --dataDirectory c:\data\resin --fileName d:\enwiki-20201026-cirrussearch-content.json.gz --collection wikipedia
    /// </example>
    public class IndexWikipediaCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var fileName = args["fileName"];
            var collection = args["collection"];
            var skip = args.ContainsKey("skip") ? int.Parse(args["skip"]) : 0;
            var take = args.ContainsKey("take") ? int.Parse(args["take"]) : int.MaxValue;
            var sampleSize = args.ContainsKey("sampleSize") ? int.Parse(args["sampleSize"]) : 1000;
            var pageSize = args.ContainsKey("pageSize") ? int.Parse(args["pageSize"]) : 100000;

            var collectionId = collection.ToHash();
            var fieldsOfInterest = new HashSet<string> { "language", "wikibase_item", "title", "text", "url" };
            var fieldsToIndex = new HashSet<string> { "title", "text" };

            if (take == 0)
                take = int.MaxValue;

            var model = new BagOfCharsModel();
            var payload = WikipediaHelper.ReadWP(fileName, skip, take, fieldsOfInterest);

            using (var sessionFactory = new Database(logger))
            {
                var debugger = new IndexDebugger(logger, sampleSize);

                using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, sessionFactory)))
                {
                    foreach (var page in payload.Batch(pageSize))
                    {
                        using (var indexStream = new WritableIndexStream(dataDirectory, collectionId, sessionFactory, logger: logger))
                        using (var indexSession = new IndexSession<string>(model, model))
                        {
                            foreach (var document in page)
                            {
                                writeSession.Put(document);

                                foreach (var field in document.Fields)
                                {
                                    if (fieldsToIndex.Contains(field.Name))
                                        indexSession.Put(document.Id, field.KeyId, (string)field.Value);
                                }

                                debugger.Step(indexSession);
                            }

                            indexStream.Write(indexSession.GetInMemoryIndex());

                            //foreach (var column in indexSession.InMemoryIndex)
                            //{
                            //    Print($"wikipedia.{column.Key}", column.Value);
                            //}
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