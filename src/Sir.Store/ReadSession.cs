using System;
using System.Collections;
using System.Collections.Generic;
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

        public IEnumerable<IDictionary> Read(Query query, int take, out long total)
        {
            IDictionary<ulong, float> result = DoRead(query);

            if (result == null)
            {
                total = 0;
                return Enumerable.Empty<IDictionary>();
            }
            else
            {
                var all = result.OrderByDescending(x => x.Value).ToList();

                total = all.Count;

                var scope = all
                    .Take(take)
                    .ToDictionary(x => x.Key, x => x.Value);

                return ReadDocs(scope);
            }
        }

        public IEnumerable<IDictionary> Read(Query query, out long total)
        {
            // Get doc IDs and their score
            IDictionary<ulong, float> result = DoRead(query);

            if (result == null)
            {
                total = 0;
                return Enumerable.Empty<IDictionary>();
            }
            else
            {
                var all = result.OrderByDescending(x => x.Value).ToList();

                total = all.Count;

                var topScore = all[0].Value;
                var scope = new List<KeyValuePair<ulong, float>>();

                foreach (var doc in all)
                {
                    if (doc.Value == topScore)
                    {
                        scope.Add(doc);
                    }
                }

                return ReadDocs(scope);
            }
        }

        public IDictionary<ulong, float> DoRead(Query query)
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
                        var match = ix.ClosestMatch(cursor.Term.Value.ToString());

                        if (match.Highscore > 0)
                        {
                            if (match.PostingsOffset < 0)
                            {
                                throw new InvalidDataException(match.ToString());
                            }

                            var docIds = _postingsReader.Read(match.PostingsOffset)
                                .ToDictionary(x => x, y => match.Highscore);

                            if (result == null)
                            {
                                result = docIds;
                            }
                            else
                            {
                                if (cursor.And)
                                {
                                    var reduced = new Dictionary<ulong, float>();

                                    foreach (var doc in result)
                                    {
                                        float score;

                                        if (docIds.TryGetValue(doc.Key, out score))
                                        {
                                            reduced[doc.Key] = 2 * (score + doc.Value);
                                        }
                                    }

                                    result = reduced;
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

        public IEnumerable<IDictionary> ReadDocs(IEnumerable<KeyValuePair<ulong, float>> docs)
        {
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

                yield return doc;
            }
        }
    }
}
