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
        private readonly SessionFactory _sessionFactory;
        private readonly IModel _model;
        private readonly IPostingsReader _postingsReader;
        private readonly IDictionary<(ulong, long), IColumnReader> _readers;
        private readonly ILogger _logger;

        public SearchSession(
            SessionFactory sessionFactory,
            IModel model,
            IPostingsReader postingsReader,
            ILogger logger = null) : base(sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _model = model;
            _postingsReader = postingsReader;
            _logger = logger ?? sessionFactory.Logger;
            _readers = new Dictionary<(ulong, long), IColumnReader>();
        }

        public SearchResult Search(Query query, int skip, int take)
        {
            var result = MapReduce(query, skip, take);

            if (result != null)
            {
                var docs = ReadDocs(result.SortedDocuments, query.Select, (double)1/query.TotalNumberOfTerms());

                return new SearchResult(query, result.Total, docs.Count, docs);
            }

            return new SearchResult(query, 0, 0, new Document[0]);
        }

        private ScoredResult MapReduce(Query query, int skip, int take)
        {
            var timer = Stopwatch.StartNew();

            // Map
            Map(query);

            _logger.LogDebug($"mapping took {timer.Elapsed}");
            timer.Restart();

            // Reduce
            IDictionary<(ulong, long), double> scoredResult = new Dictionary<(ulong, long), double>();
            _postingsReader.Reduce(query, ref scoredResult);

            _logger.LogDebug("reducing took {0}", timer.Elapsed);
            timer.Restart();

            // Sort
            var sorted = Sort(scoredResult, skip, take);

            _logger.LogDebug("sorting took {0}", timer.Elapsed);

            return sorted;
        }

        /// <summary>
        /// Map query terms to posting list locations.
        /// </summary>
        private void Map(Query query)
        {
            if (query == null)
                return;

            //Parallel.ForEach(query.AllTerms(), term =>
            foreach (var term in query.AllTerms())
            {
                var columnReader = CreateColumnReader(term.CollectionId, term.KeyId);

                if (columnReader != null)
                {
                    var hit = columnReader.ClosestMatch(term.Vector, _model);

                    if (hit != null)
                    {
                        term.Score = hit.Score;
                        term.PostingsOffsets = hit.Node.PostingsOffsets ?? new List<long> { hit.Node.PostingsOffset };
                    }
                }
            }//);
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

        public IColumnReader CreateColumnReader(ulong collectionId, long keyId)
        {
            var ixFileName = Path.Combine(_sessionFactory.Directory, string.Format("{0}.{1}.ix", collectionId, keyId));

            if (!File.Exists(ixFileName))
                return null;

            //IColumnReader reader;
            //var key = (collectionId, keyId);

            //if (!_readers.TryGetValue(key, out reader))
            //{
            //    var vectorFileName = Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.vec");
            //    var pageIndexFileName = Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.ixtp");

            //    using (var pageIndexReader = new PageIndexReader(_sessionFactory.CreateReadStream(pageIndexFileName)))
            //    {
            //        reader = new ColumnReader(
            //            pageIndexReader.ReadAll(),
            //            _sessionFactory.CreateReadStream(ixFileName),
            //            _sessionFactory.CreateReadStream(vectorFileName),
            //            _sessionFactory,
            //            _logger);
            //    }

            //    _readers.Add(key, reader);
            //}

            //return reader;

            var vectorFileName = Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.vec");
            var pageIndexFileName = Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.ixtp");

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

            _postingsReader.Dispose();
        }
    }
}