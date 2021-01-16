using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using System.Collections.Generic;

namespace Sir.Wikipedia
{
    /// <summary>
    /// Download JSON search index dump here: 
    /// https://dumps.wikimedia.org/other/cirrussearch/current/enwiki-20201026-cirrussearch-content.json.gz
    /// </summary>
    /// <example>
    /// writewikipedia --dataDirectory c:\data\resin --fileName d:\enwiki-20201026-cirrussearch-content.json.gz --collection wikipedia
    /// </example>
    public class WriteWikipediaCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var fileName = args["fileName"];
            var collection = args["collection"];
            var skip = args.ContainsKey("skip") ? int.Parse(args["skip"]) : 0;
            var take = args.ContainsKey("take") ? int.Parse(args["take"]) : int.MaxValue;
            var sampleSize = args.ContainsKey("sampleSize") ? int.Parse(args["sampleSize"]) : 1000;

            var collectionId = collection.ToHash();
            var fieldsOfInterest = new HashSet<string> { "language", "wikibase_item", "title", "text", "url" };

            if (take == 0)
                take = int.MaxValue;

            var payload = WikipediaHelper.ReadWP(fileName, skip, take, fieldsOfInterest);

            using (var sessionFactory = new Database(logger))
            {
                sessionFactory.Truncate(dataDirectory, collectionId);

                var debugger = new BatchDebugger(logger, sampleSize);

                using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, sessionFactory)))
                {
                    foreach (var document in payload)
                    {
                        writeSession.Put(document);

                        debugger.Step();
                    }
                }
            }
        }
    }
}