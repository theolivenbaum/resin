using Microsoft.Extensions.Logging;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Search
{
    /// <summary>
    /// Read session targeting multiple collections.
    /// </summary>
    public class SearchSession : DocumentStreamSession, IDisposable, ISearchSession
    {
        private readonly StreamFactory _sessionFactory;
        private readonly IModel _model;
        private readonly ILogger _logger;

        public SearchSession(
            string directory, 
            StreamFactory sessionFactory,
            IModel model,
            ILogger logger = null) : base(directory, sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _model = model;
            _logger = logger;
        }

        public SearchResult Search(Query query, int skip, int take)
        {
            var result = ScanResolveReduceSort(query, skip, take);

            if (result != null)
            {
                var docs = ReadDocs(result.SortedDocuments, query.Select, (double)1/query.TotalNumberOfTerms());

                return new SearchResult(query, result.Total, docs.Count, docs);
            }

            return new SearchResult(query, 0, 0, new Document[0]);
        }

        private ScoredResult ScanResolveReduceSort(Query query, int skip, int take)
        {
            var timer = Stopwatch.StartNew();

            // Scan
            Scan(query);
            _logger.LogDebug($"scanning took {timer.Elapsed}");
            timer.Restart();

            // Resolve
            Resolver.Resolve(query, _sessionFactory);
            _logger.LogDebug($"resolving took {timer.Elapsed}");
            timer.Restart();

            // Reduce
            IDictionary<(ulong, long), double> scoredResult = new Dictionary<(ulong, long), double>();
            Reducer.Reduce(query, ref scoredResult);
            _logger.LogDebug("reducing took {0}", timer.Elapsed);
            timer.Restart();

            // Sort
            var sorted = Sort(scoredResult, skip, take);
            _logger.LogDebug("sorting took {0}", timer.Elapsed);

            return sorted;
        }

        /// <summary>
        /// Score each term and find their posting list locations.
        /// </summary>
        private void Scan(Query query)
        {
            if (query == null)
                return;

            Parallel.ForEach(query.AllTerms(), term =>
            //foreach (var term in query.AllTerms())
            {
                var columnReader = GetColumnReader(term.Directory, term.CollectionId, term.KeyId);

                if (columnReader != null)
                {
                    using (columnReader)
                    {
                        var hit = columnReader.ClosestMatch(term.Vector, _model);

                        if (hit != null)
                        {
                            term.Score = hit.Score;
                            term.PostingsOffsets = hit.Node.PostingsOffsets ?? new List<long> { hit.Node.PostingsOffset };
                        }
                    }
                }
            });
        }

        private static ScoredResult Sort(IDictionary<(ulong, long), double> documents, int skip, int take)
        {
            var sortedByScore = new List<KeyValuePair<(ulong, long), double>>(documents);

            sortedByScore.Sort(
                delegate (KeyValuePair<(ulong, long), double> pair1,
                KeyValuePair<(ulong, long), double> pair2)
                {
                    return pair2.Value.CompareTo(pair1.Value);
                }
            );

            var index = skip > 0 ? skip : 0;
            int count;

            if (take == 0)
                count = sortedByScore.Count - (index);
            else
                count = Math.Min(sortedByScore.Count - (index), take);

            return new ScoredResult 
            { 
                SortedDocuments = sortedByScore.GetRange(index, count), 
                Total = sortedByScore.Count 
            };
        }

        private IList<Document> ReadDocs(
            IEnumerable<KeyValuePair<(ulong collectionId, long docId), double>> docIds, 
            HashSet<string> select,
            double scoreMultiplier = 1)
        {
            var result = new List<Document>();
            var timer = Stopwatch.StartNew();

            foreach (var d in docIds)
            {
                var doc = ReadDoc(d.Key, select, d.Value * scoreMultiplier);
                result.Add(doc);
            }

            _logger.LogDebug($"reading documents took {timer.Elapsed}");

            return result;
        }

        private IColumnReader GetColumnReader(string directory, ulong collectionId, long keyId)
        {
            var ixFileName = Path.Combine(directory, string.Format("{0}.{1}.ix", collectionId, keyId));

            if (!File.Exists(ixFileName))
                return null;

            var vectorFileName = Path.Combine(directory, $"{collectionId}.{keyId}.vec");
            var pageIndexFileName = Path.Combine(directory, $"{collectionId}.{keyId}.ixtp");

            using (var pageIndexReader = new PageIndexReader(_sessionFactory.CreateReadStream(pageIndexFileName)))
            {
                return new ColumnReader(
                    pageIndexReader.ReadAll(),
                    _sessionFactory.CreateReadStream(ixFileName),
                    _sessionFactory.CreateReadStream(vectorFileName),
                    _sessionFactory,
                    _logger);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}