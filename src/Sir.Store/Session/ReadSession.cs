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
        private readonly IConfigurationProvider _config;

        public ReadSession(string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config,
            ConcurrentDictionary<long, NodeReader> indexReaders) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocMapReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config, collectionName);
            _indexReaders = indexReaders;
            _config = config;
        }

        public ReadResult Read(Query query)
        {
            if (SessionFactory.CollectionExists(query.Collection))
            {
                var result = Execute(query);

                if (result != null)
                {
                    var docs = ReadDocs(result.Documents);

                    return new ReadResult { Total = result.Total, Docs = docs };
                }
            }

            this.Log("found nothing for query {0}", query);

            return new ReadResult { Total = 0, Docs = new IDictionary[0] };
        }

        public IEnumerable<long> ReadIds(Query query)
        {
            if (SessionFactory.CollectionExists(query.Collection))
            {
                var result = Execute(query);

                if (result == null)
                {
                    this.Log("found nothing for query {0}", query);

                    return Enumerable.Empty<long>();
                }

                return result.Documents.Keys;
            }

            return new long[0];
        }

        private ScoredResult Execute(Query query)
        {
            Map(query);

            var timer = Stopwatch.StartNew();

            var result = _postingsReader.Reduce(query);

            this.Log("reducing {0} to {1} docs took {2}", query, result.Documents.Count, timer.Elapsed);

            return result;
        }

        private void Map(Query query)
        {
            this.Log("before map: " + query.ToDiagram());

            var clauses = query.ToList();

            foreach (var q in clauses)
            {
                var cursor = q;

                while (cursor != null)
                {
                    BOCHit hit = null;

                    var indexReader = cursor.Term.KeyId.HasValue ?
                        CreateIndexReader(cursor.Term.KeyId.Value) :
                        CreateIndexReader(cursor.Term.KeyHash);

                    if (indexReader != null)
                    {
                        var termVector = cursor.Term.ToCharVector();

                        hit = indexReader.ClosestMatch(termVector);
                    }

                    if (hit != null)
                    {
                        cursor.Score = hit.Score;

                        foreach (var offs in hit.PostingsOffsets)
                        {
                            if (offs < 0)
                            {
                                throw new DataMisalignedException();
                            }

                            cursor.PostingsOffsets.Add(offs);
                        }
                    }

                    cursor = cursor.Then;
                }
            }

            this.Log("after map: " + query.ToDiagram());
        }

        private static readonly object _syncIndexReaderCreation = new object();

        public NodeReader CreateIndexReader(long keyId)
        {
            NodeReader reader;

            if (!_indexReaders.TryGetValue(keyId, out reader))
            {
                lock (_syncIndexReaderCreation)
                {
                    if (!_indexReaders.TryGetValue(keyId, out reader))
                    {
                        var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));
                        var ixpFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", CollectionId, keyId));
                        var vecFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId));

                        reader = new NodeReader(ixFileName, ixpFileName, vecFileName, SessionFactory, _config);

                        _indexReaders.GetOrAdd(keyId, reader);
                    }
                }
            }

            return reader;
        }

        public NodeReader CreateIndexReader(ulong keyHash)
        {
            long keyId;
            if (!SessionFactory.TryGetKeyId(CollectionId, keyHash, out keyId))
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

            return result
                .GroupBy(x => (long)x["__docid"])
                .SelectMany(g=>g.OrderByDescending(x=>(long)x["_created"]).Take(1))
                .ToList();
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
