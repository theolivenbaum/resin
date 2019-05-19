using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

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
        }

        public void Put(long docId, long keyId, string value)
        {
            AnalyzedString tokens = _tokenizer.Tokenize(value);

            BuildModel(docId, keyId, tokens);
        }

        private void BuildModel(long docId, long keyId, AnalyzedString tokens)
        {
            var ix = GetOrCreateIndex(keyId);

            foreach (var vector in tokens.Embeddings)
            {
                if (!VectorNodeWriter.Add(ix, new VectorNode(vector, docId), Similarity.Term))
                {
                    _merges++;
                }
            }
        }

        public async Task Commit()
        {
            if (_committing || _committed)
                return;

            _committing = true;

            this.Log($"merges: {_merges}");

            foreach (var column in _dirty)
            {
                using (var vectorStream = SessionFactory.CreateAppendStream(
                    Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.vec")))
                {
                    using (var writer = new ColumnSerializer(
                        CollectionId, column.Key, SessionFactory, new RemotePostingsWriter(_config, CollectionName)))
                    {
                        await writer.CreateColumnSegment(column.Value, vectorStream);
                    }
                }
            }

            _committed = true;
            _committing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private void Validate((long keyId, long docId, AnalyzedString tokens) item)
        {
            if (item.keyId == 4 || item.keyId == 5)
            {
                var tree = GetOrCreateIndex(item.keyId);

                foreach (var vector in item.tokens.Embeddings)
                {
                    var hit = VectorNodeReader.ClosestMatch(tree, vector, Similarity.Term.foldAngle);

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