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

namespace Resin
{
    /// <summary>
    /// Query the youngest index in a directory.
    /// </summary>
    public class Searcher : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringScheme _scorer;
        private readonly IxInfo _ix;
        private readonly DocumentReader _docReader;
        private readonly int _blockSize;

        public Searcher(string directory, QueryParser parser, IScoringScheme scorer)
        {
            _directory = directory;
            _parser = parser;
            _scorer = scorer;

            _ix = IxInfo.Load(GetIndexFileNamesInChronologicalOrder().Last());

            var docFileName = Path.Combine(_directory, _ix.Name + ".doc");

            _docReader = new DocumentReader(
                new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 4, FileOptions.SequentialScan));

            _blockSize = sizeof(long) + sizeof(int);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var searchTime = new Stopwatch();
            searchTime.Start();
            var parseTime = new Stopwatch();
            parseTime.Start();

            var queryContext = _parser.Parse(query);

            Log.DebugFormat("parsed query {0} in {1}", queryContext, parseTime.Elapsed);

            if (queryContext == null)
            {
                return new Result { Docs = new List<Document>() };
            }

            var skip = page * size;
            var scored = Collect(queryContext);
            var paged = scored.Skip(skip).Take(size).ToList();

            var docTime = new Stopwatch();
            docTime.Start();

            var docs = GetDocs(paged);

            Log.DebugFormat("fetched {0} docs for query {1} in {2}", docs.Count, queryContext, docTime.Elapsed);

            var result = new Result { Docs = docs, Total = scored.Count };

            Log.DebugFormat("searched {0} in {1}", queryContext, searchTime.Elapsed);

            return result;
        }

        private string[] GetIndexFileNamesInChronologicalOrder()
        {
            return Directory.GetFiles(_directory, "*.ix").OrderBy(s => s).ToArray();
        }

        private IList<DocumentScore> Collect(QueryContext query)
        {
            using (var collector = new Collector(_directory, _ix, _scorer))
            {
                return collector.Collect(query);
            }
        }

        private IList<Document> GetDocs(IList<DocumentScore> scores)
        {
            var docs = new List<KeyValuePair<double,Document>>();
            var dic = scores.ToDictionary(x => x.DocumentId, y => y.Score);

            using (var docAddressReader = new DocumentAddressReader(new FileStream(Path.Combine(_directory, _ix.Name + ".da"), FileMode.Open, FileAccess.Read, FileShare.Read, 4096*1, FileOptions.SequentialScan)))
            {
                var adrs = scores
                    .Select(s => new BlockInfo(s.DocumentId*_blockSize, _blockSize))
                    .OrderBy(b => b.Position)
                    .ToList();

                var docAdrs = docAddressReader.Get(adrs).ToList();

                foreach (var doc in _docReader.Get(docAdrs))
                {
                    var score = dic[doc.Id];
                    doc.Fields["__score"] = score.ToString(CultureInfo.InvariantCulture);
                    docs.Add(new KeyValuePair<double, Document>(score, doc));
                }
            }
            return docs.OrderByDescending(kvp=>kvp.Key).Select(kvp=>kvp.Value).ToList();
        }

        public void Dispose()
        {
        }
    }
}