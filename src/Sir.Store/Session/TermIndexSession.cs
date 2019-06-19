using Sir.Core;
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
        private readonly ProducerConsumerQueue<(BigInteger, ulong, string)> _builder;

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

            var numThreads = int.Parse(_config.Get("write_thread_count"));

            _builder = new ProducerConsumerQueue<(BigInteger, ulong, string)>(numThreads, BuildModel);
        }

        public void Put(BigInteger docId, ulong keyId, string value)
        {
            _builder.Enqueue((docId, keyId, value));
        }

        private void BuildModel((BigInteger docId, ulong keyId, string value) item)
        {
            var ix = GetOrCreateIndex(item.keyId);
            var tokens = _model.Tokenize(item.value);

            foreach (var vector in tokens.Embeddings)
            {
                if (!GraphBuilder.Add(ix, new VectorNode(vector, item.docId), _model))
                {
                    _merges++;
                }
            }
        }

        public void CommitToDisk()
        {
            _builder.Dispose();

            if (_dirty.Count == 0)
            {
                return;
            }

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