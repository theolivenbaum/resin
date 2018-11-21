using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class ReadSession : CollectionSession
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly RemotePostingsReader _postingsReader;
        private readonly StreamWriter _log;

        public ReadSession(string collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            IConfigurationService config) 
            : base(collectionId, sessionFactory)
        {
            var collection = collectionId.ToHash();

            ValueStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collection)));
            KeyStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collection)));
            DocStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collection)));
            ValueIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collection)));
            KeyIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collection)));
            DocIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collection)));
            PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collection)));
            Index = sessionFactory.GetCollectionIndex(collectionId.ToHash());

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config);

            _log = Logging.CreateWriter("readsession");
        }

        public async Task<ReadResult> Read(Query query, int take)
        {
            var timer = new Stopwatch();
            timer.Start();

            IDictionary<ulong, float> result = await DoRead(query);

            if (result == null)
            {
                _log.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                _log.Log("read {0} postings for query {1} in {2}",
                    result.Count, query, timer.Elapsed);

                timer.Restart();

                IEnumerable<KeyValuePair<ulong, float>> sorted = result.OrderByDescending(x => x.Value);
                
                if (take > 0)
                    sorted = sorted.Take(take);

                _log.Log("sorted {0} postings for query {1} in {2}",
                    result.Count, query, timer.Elapsed);

                var docs = await ReadDocs(sorted);

                return new ReadResult { Total = result.Count, Docs = docs };
            }
        }

        public async Task<ReadResult> Read(Query query)
        {
            var timer = new Stopwatch();

            // Get doc IDs and their score
            IDictionary<ulong, float> result = await DoRead(query);

            if (result == null)
            {
                _log.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                if (result.Count < 101)
                {
                    return new ReadResult { Total = result.Count, Docs = await ReadDocs(result) };
                }

                timer.Restart();

                var sorted = new List<KeyValuePair<ulong, float>>();
                var ordered = result.OrderByDescending(x => x.Value);
                float topScore = 0;
                int index = 0;

                foreach (var s in ordered)
                {
                    if (index++ == 0)
                    {
                        topScore = s.Value;
                        sorted.Add(s);
                    }
                    else if (s.Value == topScore)
                    {
                        sorted.Add(s);
                    }
                    else
                    {
                        break;
                    }
                }

                _log.Log("sorted and reduced {0} postings for query {1} in {2}",
                    result.Count, query, timer.Elapsed);

                return new ReadResult { Total = result.Count, Docs = await ReadDocs(sorted) };
            }
        }

        private async Task<IDictionary<ulong, float>> DoRead(Query query)
        {
            try
            {
                IDictionary<ulong, float> result = null;

                var cursor = query;

                while (cursor != null)
                {
                    var keyHash = cursor.Term.Key.ToString().ToHash();
                    var ix = GetIndex(keyHash);

                    if (ix != null)
                    {
                        var queryTerm = new VectorNode(cursor.Term.Value.ToString());

                        var match = ix.ClosestMatch(queryTerm);

                        if (match.Highscore > 0)
                        {
                            if (match.PostingsOffset < 0)
                            {
                                throw new InvalidDataException();
                            }

                            var docIds = (await _postingsReader.Read(CollectionId, match.PostingsOffset))
                                .ToDictionary(x => x, y => match.Highscore);

                            if (result == null)
                            {
                                result = docIds;
                            }
                            else
                            {
                                if (cursor.And)
                                {
                                    var aggregatedResult = new Dictionary<ulong, float>();

                                    foreach (var doc in result)
                                    {
                                        float score;

                                        if (docIds.TryGetValue(doc.Key, out score))
                                        {
                                            aggregatedResult[doc.Key] = score + doc.Value;
                                        }
                                    }

                                    result = aggregatedResult;
                                }
                                else if (cursor.Not)
                                {
                                    foreach (var id in docIds.Keys)
                                    {
                                        result.Remove(id);
                                    }
                                }
                                else // Or
                                {
                                    foreach (var id in docIds)
                                    {
                                        float score;

                                        if (result.TryGetValue(id.Key, out score))
                                        {
                                            result[id.Key] = score + id.Value;
                                        }
                                        else
                                        {
                                            result.Add(id.Key, id.Value);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    cursor = cursor.Next;
                }
                _log.Log("query {0} matched {1} docs", query.Term, result == null ? 0 : result.Count);

                return result;
            }
            catch (Exception ex)
            {
                _log.Log(ex);
                throw;
            }
        }

        private async Task<IList<IDictionary>> ReadDocs(IEnumerable<KeyValuePair<ulong, float>> docs)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<IDictionary>();

            foreach (var d in docs)
            {
                var docInfo = await _docIx.ReadAsync(d.Key);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = await _docs.Read(docInfo.offset, docInfo.length);
                var doc = new Dictionary<IComparable, IComparable>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = await _keyIx.ReadAsync(kvp.keyId);
                    var vInfo = await _valIx.ReadAsync(kvp.valId);
                    var key = await _keyReader.ReadAsync(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = await _valReader.ReadAsync(vInfo.offset, vInfo.len, vInfo.dataType);

                    doc[key] = val;
                }

                doc["__docid"] = d.Key;
                doc["__score"] = d.Value;

                result.Add(doc);
            }

            _log.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }
    }

    public class ReadResult
    {
        public long Total { get; set; }
        public IList<IDictionary> Docs { get; set; }
    }
}
