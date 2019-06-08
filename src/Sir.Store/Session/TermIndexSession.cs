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
        private readonly ITokenizer _tokenizer;
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;
        private bool _committed;
        private bool _committing;
        private long _merges;
        private readonly ProducerConsumerQueue<(long, long, string)> _builder;

        public TermIndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
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
            var tokens = _tokenizer.Tokenize(workItem.value);

            foreach (var vector in tokens.Embeddings)
            {
                if (!GraphSerializer.Add(ix, new VectorNode(vector, workItem.docId), Similarity.Term))
                {
                    _merges++;
                }
            }
        }

        public void Commit()
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
                            writer.CreateColumnSegment(column.Value, vectorStream, postingsStream);
                        }
                    }
                }
            }

            _committed = true;
            _committing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private void Validate((long keyId, long docId, AnalyzedString tokens) item)
        {
            var tree = GetOrCreateIndex(item.keyId);

            foreach (var vector in item.tokens.Embeddings)
            {
                var hit = PathFinder.ClosestMatch(tree, vector, Similarity.Term.foldAngle);

                if (hit.Score < Similarity.Term.identicalAngle)
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