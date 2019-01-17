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
    public class ReadSession : DocumentSession
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly RemotePostingsReader _postingsReader;

        public ReadSession(string collectionId, 
            SessionFactory sessionFactory, 
            IConfigurationProvider config) 
            : base(collectionId, sessionFactory)
        {
            var collection = collectionId.ToHash();

            ValueStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collection)));
            KeyStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collection)));
            DocStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collection)));
            ValueIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collection)));
            KeyIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collection)));
            DocIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collection)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config);
        }

        public async Task<ReadResult> Read(Query query, int take)
        {
            IDictionary<ulong, float> result = MapReduce(query);

            if (result == null)
            {
                Logging.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                IEnumerable<KeyValuePair<ulong, float>> sorted = result.OrderByDescending(x => x.Value);
                
                if (take > 0)
                    sorted = sorted.Take(take);

                var docs = await ReadDocs(sorted);

                return new ReadResult { Total = result.Count, Docs = docs };
            }
        }

        public async Task<ReadResult> Read(Query query)
        {
            var timer = new Stopwatch();

            // Get doc IDs and their score
            IDictionary<ulong, float> docIds = MapReduce(query);

            if (docIds == null)
            {
                Logging.Log("found nothing for query {0}", query);

                return new ReadResult { Total = 0, Docs = new IDictionary[0] };
            }
            else
            {
                if (docIds.Count < 101)
                {
                    return new ReadResult { Total = docIds.Count, Docs = await ReadDocs(docIds) };
                }

                timer.Restart();

                var sorted = new List<KeyValuePair<ulong, float>>();
                var ordered = docIds.OrderByDescending(x => x.Value);
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

                Logging.Log("sorted and reduced {0} postings for query {1} in {2}",
                    docIds.Count, query, timer.Elapsed);

                var docs = await ReadDocs(sorted);

                Logging.Log("read {0} documents from disk for query {1} in {2}",
                    docs.Count, query, timer.Elapsed);

                return new ReadResult { Total = docIds.Count, Docs = docs };
            }
        }

        private IDictionary<ulong, float> MapReduce(Query query)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                Map(query);

                Logging.Log("index scan for query {0} took {1}", query, timer.Elapsed);

                timer.Restart();

                var docIds =  _postingsReader.Reduce(CollectionId, query.ToStream());

                Logging.Log("reducing {0} to {1} docs took {2}",
                    query, docIds.Count, timer.Elapsed);

                return docIds;
            }
            catch (Exception ex)
            {
                Logging.Log(ex);
                throw;
            }
        }

        private void Map(Query query)
        {
            foreach (var cursor in query.ToList())
            {
                var timer = new Stopwatch();
                timer.Start();

                var keyHash = cursor.Term.Key.ToString().ToHash();
                IList<Hit> hits = null;
                var indexReader = CreateIndexReader(keyHash);

                if (indexReader != null)
                {
                    var termVector = cursor.Term.ToCharVector();

                    hits = indexReader.ClosestMatch(termVector);
                }

                Logging.Log("scan found {0} matches in {1}", hits.Count, timer.Elapsed);

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
                            cursor.InsertAfter(new Query { Score = hit.Score, PostingsOffset = hit.PostingsOffset });
                        }
                    }

                    Logging.Log("sorted and mapped term {0} in {1}", cursor, timer.Elapsed);
                }
            }
        }

        private NodeReader CreateIndexReader(long keyId)
        {
            var cid = CollectionId.ToHash();
            var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", cid, keyId));
            var pageIxFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", cid, keyId));
            var vecFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", cid));

            var ixpStream = SessionFactory.CreateAsyncReadStream(pageIxFileName);

            return new NodeReader(ixFileName, vecFileName, ixpStream, SessionFactory);
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

            Logging.Log("read {0} docs in {1}", result.Count, timer.Elapsed);

            return result;
        }

        public override void Dispose()
        {
            base.Dispose();

            _postingsReader.Dispose();
            Logging.Close();
        }
    }
}
