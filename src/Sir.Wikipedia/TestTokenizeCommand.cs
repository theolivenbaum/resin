using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using System.Collections.Generic;

namespace Sir.Wikipedia
{
    public class TestTokenizeCommand : ICommand
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
            var fieldsToStore = new HashSet<string> { "language", "wikibase_item", "title", "text" };
            var fieldsToIndex = new HashSet<string> { "language", "title", "text" };

            if (take == 0)
                take = int.MaxValue;

            var model = new BagOfCharsModel();
            var payload = WikipediaHelper.ReadWP(fileName, skip, take, fieldsToStore, fieldsToIndex);
            var debugger = new BatchDebugger(logger, sampleSize);

            using (var sessionFactory = new StreamFactory(logger))
            {
                using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, sessionFactory)))
                {
                    foreach (var page in payload.Batch(pageSize))
                    {
                        using (var indexSession = new IndexSession<string>(model, model))
                        {
                            foreach (var document in page)
                            {
                                writeSession.Put(document);

                                foreach (var field in document.IndexableFields)
                                {
                                    foreach (var token in model.Tokenize((string)field.Value))
                                    {
                                        debugger.Step();
                                    }
                                }
                            }
                        }
                    }

                    logger.LogInformation($"tokenized {debugger.StepCount} in {debugger.Time}.");
                }
            }
        }
    }
}