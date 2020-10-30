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
    public class QuerySession : DocumentStreamSession, IDisposable, IQuerySession
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IModel _model;
        private readonly IPostingsReader _postingsReader;

        private readonly ILogger _logger;

        public QuerySession(
            SessionFactory sessionFactory,
            IModel model,
            IPostingsReader postingsReader,
            ILogger logger) : base(sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _model = model;
            _postingsReader = postingsReader;
            _logger = logger;
        }

        public ReadResult Query(IQuery query, int skip, int take, string primaryKey = null)
        {
            var result = MapReduceSort(query, skip, take);

            if (result != null)
            {
                var docs = ReadDocs(result.SortedDocuments, query.Select, primaryKey);

                return new ReadResult { Query = query, Total = result.Total, Documents = docs };
            }

            return new ReadResult { Query = query, Total = 0, Documents = new IDictionary<string, object>[0] };
        }

        public ReadResult Query(Term term, int skip, int take, HashSet<string> select)
        {
            var result = MapReduceSort(term, skip, take);

            if (result != null)
            {
                var docs = ReadDocs(result.SortedDocuments, select);

                return new ReadResult { QueryTerm = term, Total = result.Total, Documents = docs };
            }

            return new ReadResult { QueryTerm = term, Total = 0, Documents = new IDictionary<string, object>[0] };
        }

        private ScoredResult MapReduceSort(IQuery query, int skip, int take)
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

        private ScoredResult MapReduceSort(Term term, int skip, int take)
        {
            var timer = Stopwatch.StartNew();

            // Map
            Map(term);

            _logger.LogDebug($"scanning took {timer.Elapsed}");
            timer.Restart();

            // Reduce
            IDictionary<(ulong, long), double> scoredResult = new Dictionary<(ulong, long), double>();
            _postingsReader.Reduce(term, ref scoredResult);

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
        private void Map(IQuery query)
        {
            if (query == null)
                return;

            //foreach (var q in query.All())
            Parallel.ForEach(query.All(), q =>
            {
                //foreach (var term in q.Terms)
                Parallel.ForEach(q.Terms, term =>
                {
                    var columnReader = CreateColumnReader(term.CollectionId, term.KeyId);

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
            });
        }

        private void Map(Term term)
        {
            var columnReader = CreateColumnReader(term.CollectionId, term.KeyId);

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

        private IList<IDictionary<string, object>> ReadDocs(IEnumerable<KeyValuePair<(ulong collectionId, long docId), double>> docIds, HashSet<string> select, string primaryKey = null)
        {
            if (!select.Contains(primaryKey))
            {
                select.Add(primaryKey);
            }

            var result = new List<IDictionary<string, object>>();
            var documentsByPrimaryKey = new Dictionary<ulong, IDictionary<string, object>>();
            var timer = Stopwatch.StartNew();

            foreach (var d in docIds)
            {
                var doc = ReadDoc(d.Key, select, d.Value);
                var docHash = primaryKey == null ? Guid.NewGuid().ToString().ToHash() : doc[primaryKey].ToString().ToHash();
                IDictionary<string, object> existingDoc;

                if (documentsByPrimaryKey.TryGetValue(docHash, out existingDoc))
                {
                    foreach (var field in doc)
                    {
                        // TODO: add condition for when there are two doc versions with different created dates.
                        if (field.Key != primaryKey && select.Contains(field.Key))
                        {
                            object existingValue;

                            if (existingDoc.TryGetValue(field.Key, out existingValue))
                            {
                                existingDoc[field.Key] = new object[] { existingValue, field.Value };
                            }
                            else
                            {
                                existingDoc[field.Key] = field.Value;
                            }
                        }
                    }

                    existingDoc[SystemFields.Score] = (double)existingDoc[SystemFields.Score] + (double)doc[SystemFields.Score]; 
                }
                else
                {
                    doc[SystemFields.Score] = (double)doc[SystemFields.Score];
                    result.Add(doc);
                    documentsByPrimaryKey.Add(docHash, doc);
                }
            }

            _logger.LogDebug($"reading documents took {timer.Elapsed}");
            timer.Restart();

            result.Sort(
                delegate (IDictionary<string, object> doc1,
                IDictionary<string, object> doc2)
                {
                    return ((double)doc2[SystemFields.Score]).CompareTo((double)doc1[SystemFields.Score]);
                });

            _logger.LogDebug($"second sorting took {timer.Elapsed}");

            return result;
        }

        public IColumnReader CreateColumnReader(ulong collectionId, long keyId)
        {
            var ixFileName = Path.Combine(_sessionFactory.Directory, string.Format("{0}.{1}.ix", collectionId, keyId));

            if (!File.Exists(ixFileName))
                return null;

            var vectorFileName = Path.Combine(_sessionFactory.Directory, $"{collectionId}.vec");

            return new ColumnStreamReader(
                    new PageIndexReader(_sessionFactory.CreateReadStream(Path.Combine(_sessionFactory.Directory, $"{collectionId}.{keyId}.ixtp"))),
                    _sessionFactory.CreateReadStream(ixFileName),
                    _sessionFactory.CreateReadStream(vectorFileName),
                    _sessionFactory,
                    _logger);
        }

        public override void Dispose()
        {
            base.Dispose();

            _postingsReader.Dispose();
        }
    }
}
