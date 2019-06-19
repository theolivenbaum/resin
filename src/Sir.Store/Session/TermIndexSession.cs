using Sir.Core;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;
        private bool _committed;
        private bool _committing;
        private long _merges;
        private readonly ProducerConsumerQueue<(long, long, string)> _builder;

        public TermIndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _model = tokenizer;
            _dirty = new ConcurrentDictionary<long, VectorNode>();

            var numThreads = int.Parse(_config.Get("write_thread_count"));

            _builder = new ProducerConsumerQueue<(long, long, string)>(numThreads, BuildModel);
        }

        public void Put(long docId, long keyId, string value)
        {
            _builder.Enqueue((docId, keyId, value));
        }

        private void BuildModel((long docId, long keyId, string value) workItem)
        {
            var ix = GetOrCreateIndex(workItem.keyId);
            var tokens = _model.Tokenize(workItem.value);

            foreach (var vector in tokens.Embeddings)
            {
                if (!GraphBuilder.Add(ix, new VectorNode(vector, workItem.docId), _model))
                {
                    _merges++;
                }
            }
        }

        public void CommitToDisk()
        {
            if (_committing || _committed)
                return;

            _committing = true;

            _builder.Dispose();

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

            _committed = true;
            _committing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private void Validate((long keyId, long docId, AnalyzedData tokens) item)
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

        private VectorNode GetOrCreateIndex(long keyId)
        {
            return _dirty.GetOrAdd(keyId, new VectorNode());
        }

        public void Dispose()
        {
            CommitToDisk();
        }
    }
}