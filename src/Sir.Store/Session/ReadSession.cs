using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class ReadSession : DocumentSession, ILogger
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly RemotePostingsReader _postingsReader;
        private readonly ConcurrentDictionary<long, NodeReader> _indexReaders;

        public ReadSession(string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config);
            _indexReaders = new ConcurrentDictionary<long, NodeReader>();
        }

        public async Task<ReadResult> Read(Query query)
        {
            var result = Reduce(query);

            if (result == null)
            {
                this.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                var docs = await ReadDocs(result.Documents);

                return new ReadResult { Total = result.Total, Docs = docs };
            }
        }

        private ScoredResult Reduce(Query query)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                Scan(query);

                this.Log("index scan for query {0} took {1}", query, timer.Elapsed);

                timer.Restart();

                var result =  _postingsReader.Reduce(CollectionName, query.ToStream(), query.Skip, query.Take);

                this.Log("reducing {0} to {1} docs took {2}",
                    query, result.Documents.Count, timer.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                this.Log(ex);
                throw;
            }
        }

        private void Scan(Query query)
        {
            Debug.WriteLine("before");
            Debug.WriteLine(query.ToDiagram());

            //foreach (var cursor in query.ToList())
            Parallel.ForEach(query.ToList(), cursor =>
            {
                // score each query term

                var timer = new Stopwatch();
                timer.Start();

                var keyHash = cursor.Term.KeyHash;
                IList<Hit> hits = null;

                var indexReader = cursor.Term.KeyId.HasValue ? 
                    CreateIndexReader(cursor.Term.KeyId.Value) : 
                    CreateIndexReader(keyHash);

                if (indexReader != null)
                {
                    var termVector = cursor.Term.ToCharVector();

                    hits = indexReader.ClosestMatch(termVector);
                }

                this.Log("scan found {0} matches in {1}", hits.Count, timer.Elapsed);

                if (hits.Count > 0)
                {
                    timer.Restart();

                    var topHits = hits.OrderByDescending(x => x.Score).ToList();
                    var topHit = topHits.First();

                    cursor.Score = topHit.Score;
                    cursor.PostingsOffset = topHit.PostingsOffset;

                    if (topHits.Count > 1)
                    {
                        foreach (var hit in topHits.Skip(1))
                        {
                            if (cursor.And && hit.Score < topHit.Score)
                            {
                                break;
                            }

                            if (hit.Score > VectorNode.TermFoldAngle)
                                cursor.InsertAfter(new Query(hit));
                        }
                    }

                    this.Log("sorted and mapped term {0} in {1}", cursor, timer.Elapsed);
                }
            });

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

        public async Task<IList<IDictionary>> ReadDocs(IEnumerable<KeyValuePair<long, float>> docs)
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

                var docMap = _docs.Read(docInfo.offset, docInfo.length);
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

            this.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }


        public async Task<IList<IDictionary>> ReadDocs(IEnumerable<long> docs)
        {
            var timer = new Stopwatch();
            timer.Start();

            var result = new List<IDictionary>();

            foreach (var d in docs)
            {
                var docInfo = await _docIx.ReadAsync(d);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _docs.Read(docInfo.offset, docInfo.length);
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

                doc["__docid"] = d;
                doc["__score"] = 1f;

                result.Add(doc);
            }

            this.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }
    }
}
