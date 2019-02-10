using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class ReadSession : DocumentSession, ILogger
    {
        private readonly DocIndexReader _docIx;
        private readonly DocMapReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly RemotePostingsReader _postingsReader;
        private readonly ConcurrentDictionary<long, NodeReader> _indexReaders;

        public ReadSession(string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config,
            ConcurrentDictionary<long, NodeReader> indexReaders) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocMapReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config, collectionName);
            _indexReaders = indexReaders;
        }

        public ReadResult Read(Query query)
        {
            var result = Execute(query);

            if (result == null)
            {
                this.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                var docs = ReadDocs(result.Documents);

                return new ReadResult { Total = result.Total, Docs = docs };
            }
        }

        public IEnumerable<long> ReadIds(Query query)
        {
            var result = Execute(query);

            if (result == null)
            {
                this.Log("found nothing for query {0}", query);

                return Enumerable.Empty<long>();
            }
            else
            {
                return result.Documents.Keys;
            }
        }

        private ScoredResult Execute(Query query)
        {
            try
            {
                Map(query);

                var timer = Stopwatch.StartNew();

                var result =  _postingsReader.Reduce(query.ToStream(), query.Skip, query.Take);

                this.Log("reducing {0} to {1} docs took {2}", query, result.Documents.Count, timer.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        private void Map(Query query)
        {
            Debug.WriteLine("before");
            Debug.WriteLine(query.ToDiagram());

            foreach (var q in query.ToList())
            //Parallel.ForEach(query.ToList(), q =>
            {
                // score each query term

                var keyHash = q.Term.KeyHash;
                IEnumerable<Hit> hits = null;

                var indexReader = q.Term.KeyId.HasValue ? 
                    CreateIndexReader(q.Term.KeyId.Value) : 
                    CreateIndexReader(keyHash);

                if (indexReader != null)
                {
                    var termVector = q.Term.ToCharVector();

                    hits = indexReader.ClosestMatch(termVector);
                }

                if (hits != null)
                {
                    var topHits = hits.OrderByDescending(x => x.Score).ToList();
                    var topHit = topHits.First();

                    q.Score = topHit.Score;
                    q.PostingsOffset = topHit.PostingsOffsets[0];

                    foreach (var offset in topHit.PostingsOffsets.Skip(1))
                    {
                        q.AddClause(new Query(topHit.Copy(), offset));
                    }

                    if (topHits.Count > 1)
                    {
                        foreach (var hit in topHits.Skip(1))
                        {
                            if (hit.Score > VectorNode.TermFoldAngle)
                            {
                                foreach (var offset in hit.PostingsOffsets)
                                {
                                    q.AddClause(new Query(topHit.Copy(), offset));
                                }
                            }
                        }
                    }
                }
            }//);

            Debug.WriteLine("after");
            Debug.WriteLine(query.ToDiagram());
        }

        public NodeReader CreateIndexReader(long keyId)
        {
            NodeReader reader;

            if (!_indexReaders.TryGetValue(keyId, out reader))
            {
                var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));
                var ixpFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", CollectionId, keyId));
                var vecFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId));

                IList<(long, long)> pages;
                using (var ixpStream = SessionFactory.CreateAsyncReadStream(ixpFileName))
                {
                    pages = new PageIndexReader(ixpStream).ReadAll();
                }

                reader = new NodeReader(ixFileName, vecFileName, SessionFactory, pages);

                _indexReaders.GetOrAdd(keyId, reader);
            }

            return reader;
        }

        public NodeReader CreateIndexReader(ulong keyHash)
        {
            long keyId;
            if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
            {
                return null;
            }

            return CreateIndexReader(keyId);
        }

        public IList<IDictionary> ReadDocs(IEnumerable<KeyValuePair<long, float>> docs)
        {
            var timer = Stopwatch.StartNew();

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

            this.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }

        public IList<IDictionary> ReadDocs(IEnumerable<long> docs)
        {
            var timer = Stopwatch.StartNew();

            var result = new List<IDictionary>();

            foreach (var d in docs)
            {
                var docInfo = _docIx.Read(d);

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

                doc["__docid"] = d;
                doc["__score"] = 1f;

                result.Add(doc);
            }

            this.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }
    }
}
