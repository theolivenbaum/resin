using System;
using System.Collections.Concurrent;
using System.IO;
using System.Numerics;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<ulong, VectorNode> _dirty;
        private long _merges;
        private static object _sync = new object();

        public TermIndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _model = tokenizer;
            _dirty = new ConcurrentDictionary<ulong, VectorNode>();
        }

        public void Put(BigInteger docId, ulong keyId, string value)
        {
            BuildModel(docId, keyId, value);
        }

        private void BuildModel(BigInteger docId, ulong keyId, string value)
        {
            var ix = GetOrCreateIndex(keyId);
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens.Embeddings)
            {
                if (!GraphBuilder.Add(ix, new VectorNode(vector, docId), _model))
                {
                    _merges++;
                }
            }
        }

        public void CommitToDisk()
        {
            lock (_sync)
            {
                this.Log($"merges: {_merges}");

                using (var postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos")))
                {
                    foreach (var column in _dirty)
                    {
                        using (var vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.vec")))
                        {
                            using (var writer = new ColumnSerializer(CollectionId, column.Key, SessionFactory))
                            {
                                writer.CreateColumnSegment(column.Value, vectorStream, postingsStream, _model);
                            }
                        }
                    }
                }

                this.Log(string.Format("***FLUSHED***"));
            }
        }

        private void Validate((ulong keyId, BigInteger docId, AnalyzedData tokens) item)
        {
            var tree = GetOrCreateIndex(item.keyId);

            foreach (var vector in item.tokens.Embeddings)
            {
                var hit = PathFinder.ClosestMatch(tree, vector, _model);

                if (hit.Score < _model.IdenticalAngle)
                {
                    throw new DataMisalignedException();
                }

                var valid = false;

                foreach (var id in hit.Node.DocIds)
                {
                    if (id == item.docId)
                    {
                        valid = true;
                        break;
                    }
                }

                if (!valid)
                {
                    throw new DataMisalignedException();
                }
            }
        }

        private VectorNode GetOrCreateIndex(ulong keyId)
        {
            return _dirty.GetOrAdd(keyId, new VectorNode());
        }

        public void Dispose()
        {
        }
    }
}