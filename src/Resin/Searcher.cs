using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.Querying;
using Resin.Sys;

namespace Resin
{
    /// <summary>
    /// Query indices in a directory.
    /// </summary>
    public class Searcher : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringScheme _scorerFactory;
        private readonly bool _compression;
        private readonly IList<IxInfo> _ixs;
        private readonly int _blockSize;
        private readonly IDictionary<string, int> _documentCount;

        public Searcher(string directory, QueryParser parser, IScoringScheme scorerFactory, bool compression = false)
        {
            _directory = directory;
            _parser = parser;
            _scorerFactory = scorerFactory;
            _compression = compression;

            _ixs = Util.GetIndexFileNamesInChronologicalOrder(directory).Select(IxInfo.Load).ToList();

            _documentCount = Util.GetDocumentCount(_ixs);

            _blockSize = Serializer.SizeOfBlock();
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var searchTime = new Stopwatch();
            searchTime.Start();

            var queryContext = _parser.Parse(query);

            if (queryContext == null)
            {
                return new Result { Docs = new List<Document>() };
            }

            var skip = page * size;
            var scored = Collect(queryContext);
            var paged = scored.Skip(skip).Take(size).ToList();
            var docs = new List<Document>();
            var result = new Result { Total = scored.Count, Docs = docs};
            var groupedByIx = paged.GroupBy(s => s.Ix);

            var docTime = new Stopwatch();
            docTime.Start(); 
            
            foreach (var group in groupedByIx)
            {
                docs.AddRange(GetDocs(group.ToList(), group.Key));
            }

            Log.DebugFormat("fetched {0} docs for query {1} in {2}", docs.Count, queryContext, docTime.Elapsed);
            Log.DebugFormat("searched {0} in {1}", queryContext, searchTime.Elapsed);

            return result;
        }

        private IList<DocumentScore> Collect(QueryContext query)
        {
            var results = new List<IList<DocumentScore>>();

            foreach (var ix in _ixs)
            {
                using (var collector = new Collector(_directory, ix, _scorerFactory, _documentCount))
                {
                    results.Add(collector.Collect(query));
                }
            }

            return results
                .Aggregate<IEnumerable<DocumentScore>, IEnumerable<DocumentScore>>(
                        null, DocumentScore.CombineOr).ToList();
        }

        private IList<Document> GetDocs(IList<DocumentScore> scores, IxInfo ix)
        {
            IList<BlockInfo> docAdrs;

            using (var docAddressReader = new DocumentAddressReader(new FileStream(Path.Combine(_directory, ix.VersionId + ".da"), FileMode.Open, FileAccess.Read, FileShare.Read, 4096*1, FileOptions.SequentialScan)))
            {
                var adrs = scores
                    .Select(s => new BlockInfo(s.DocumentId*_blockSize, _blockSize))
                    .OrderBy(b => b.Position)
                    .ToList();

                docAdrs = docAddressReader.Get(adrs).ToList();
            }

            var docFileName = Path.Combine(_directory, ix.VersionId + ".doc");

            using (var docReader = new DocumentReader(new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096*4, FileOptions.SequentialScan), _compression))
            {
                var docs = new List<KeyValuePair<double, Document>>();
                var dic = scores.ToDictionary(x => x.DocumentId, y => y.Score);

                foreach (var doc in docReader.Get(docAdrs))
                {
                    var score = dic[doc.Id];
                    doc.Fields["__score"] = score.ToString(CultureInfo.InvariantCulture);
                    docs.Add(new KeyValuePair<double, Document>(score, doc));
                }
                return docs.OrderByDescending(kvp=>kvp.Key).Select(kvp=>kvp.Value).ToList();
            }
        }

        public void Dispose()
        {
        }
    }
}