using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        private readonly PagedPostingsReader _postingsReader;
        private readonly StreamWriter _log;

        public ReadSession(ulong collectionId, LocalStorageSessionFactory sessionFactory) 
            : base(collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            VectorStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetCollectionIndex(collectionId);

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new PagedPostingsReader(PostingsStream);

            _log = Logging.CreateWriter("session");
        }

        public IList<IDictionary> Read(Query query, int take, out long total)
        {
            var timer = new Stopwatch();
            timer.Start();

            IDictionary<ulong, float> result = DoRead(query);

            if (result == null)
            {
                _log.Log("found nothing for query {0}", query);

                total = 0;
                return new IDictionary[0];
            }
            else
            {
                _log.Log("read {0} postings for query {1} in {2}",
                    result.Count, query, timer.Elapsed);

                total = result.Count;

                timer.Restart();

                var sorted = result.OrderByDescending(x => x.Value).Take(take).ToList();

                _log.Log("sorted {0} postings for query {1} in {2}",
                    result.Count, query, timer.Elapsed);

                return ReadDocs(sorted);
            }
        }

        public IList<IDictionary> Read(Query query, out long total)
        {
            var timer = new Stopwatch();

            // Get doc IDs and their score
            IDictionary<ulong, float> result = DoRead(query);

            if (result == null)
            {
                _log.Log("found nothing for query {0}", query);

                total = 0;

                return new IDictionary[0];
            }
            else
            {
                total = result.Count;

                if (total < 101)
                {
                    return ReadDocs(result);
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

                return ReadDocs(sorted);
            }
        }

        public IDictionary<ulong, float> DoRead(Query query)
        {
            try
            {
                var timer = new Stopwatch();

                IDictionary<ulong, float> result = null;

                var cursor = query;

                while (cursor != null)
                {
                    var keyHash = cursor.Term.Key.ToString().ToHash();
                    var ix = GetIndex(keyHash);

                    if (ix != null)
                    {
                        var match = ix.ClosestMatch(cursor.Term.Value.ToString());

                        if (match.Highscore > 0)
                        {
                            if (match.PostingsOffset < 0)
                            {
                                throw new InvalidDataException(match.ToString());
                            }

                            timer.Restart();

                            var docIds = _postingsReader.Read(match.PostingsOffset);

                            _log.Log("read {0} postings into memory in {1}", docIds.Count, timer.Elapsed);

                            if (result == null)
                            {
                                result = docIds.ToDictionary(x => x, y => match.Highscore);
                            }
                            else
                            {
                                if (cursor.And)
                                {
                                    var reduced = new Dictionary<ulong, float>();

                                    foreach (var docId in docIds)
                                    {
                                        float score;

                                        if (result.TryGetValue(docId, out score))
                                        {
                                            reduced[docId] = score + match.Highscore;
                                        }
                                    }

                                    result = reduced;
                                }
                                else if (cursor.Not)
                                {
                                    foreach (var id in docIds)
                                    {
                                        result.Remove(id);
                                    }
                                }
                                else // Or
                                {
                                    foreach (var docId in docIds)
                                    {
                                        float score;

                                        if (result.TryGetValue(docId, out score))
                                        {
                                            result[docId] = score + match.Highscore;
                                        }
                                        else
                                        {
                                            result.Add(docId, match.Highscore);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    cursor = cursor.Next;
                }
                _log.Log("reduced query {0} to {1} matching docs", query.Term, result == null ? 0 : result.Count);

                return result;
            }
            catch (Exception ex)
            {
                _log.Log(ex);
                throw;
            }
        }

        public IList<IDictionary> ReadDocs(IEnumerable<KeyValuePair<ulong, float>> docs)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<IDictionary>();

            foreach (var d in docs)
            {
                var docInfo = _docIx.Read(d.Key);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _docs.Read(docInfo.offset, docInfo.length);
                var doc = new Dictionary<IComparable, IComparable>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _keyIx.Read(kvp.keyId);
                    var vInfo = _valIx.Read(kvp.valId);
                    var key = _keyReader.Read(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = _valReader.Read(vInfo.offset, vInfo.len, vInfo.dataType);

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
}
