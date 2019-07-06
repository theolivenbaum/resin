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
        private readonly int _numThreads;
        private ProducerConsumerQueue<(long, long, string)> _builder;
        private Stream _postingsStream;
        private readonly Stream _vectorStream;

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
            _numThreads = int.Parse(_config.Get("index_session_thread_count"));
            _builder = new ProducerConsumerQueue<(long, long, string)>(_numThreads, BuildModel);
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
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
                GraphBuilder.Add(ix, new VectorNode(vector, workItem.docId), _model);
            }
        }

        public void Flush()
        {
            _builder.Dispose();

            foreach (var column in _dirty)
            {
                using (var writer = new ColumnSerializer(CollectionId, column.Key, SessionFactory))
                {
                    writer.CreatePage(column.Value, _vectorStream, _postingsStream, _model);
                }
            };

            _postingsStream.Flush();
            _vectorStream.Flush();

            this.Log(string.Format("***FLUSHED***"));

            _dirty.Clear();
            _builder = new ProducerConsumerQueue<(long, long, string)>(_numThreads, BuildModel);
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
        }
    }
}