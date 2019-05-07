using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ProducerConsumerQueue<(long docId, IDictionary doc)> _indexBuilder;

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

            _indexBuilder = new ProducerConsumerQueue<(long docId, IDictionary doc)>(
                numThreads, ProcessDocument);
        }

        /// <summary>
        /// Fields prefixed with "___" or "__" will not be indexed.
        /// Fields prefixed with "_" will not be tokenized.
        /// </summary>
        public void Put(long docId, IDictionary doc)
        {
            _indexBuilder.Enqueue((docId, doc));
        }

        public void ProcessDocument((long docId, IDictionary doc) workItem)
        {
            foreach (var obj in workItem.doc.Keys)
            {
                var key = obj.ToString();

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(CollectionId, keyHash);
                    var val = workItem.doc[key];
                    var str = val as string;
                    AnalyzedString tokens = null;

                    if (str == null || key[0] == '_')
                    {
                        var v = val.ToString();

                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            tokens = new AnalyzedString(
                                new List<(int, int)> { (0, v.Length) },
                                new List<SortedList<long, int>> { v.ToVector() },
                                v);
                        }
                    }
                    else
                    {
                        tokens = _tokenizer.Tokenize(str);
                    }

                    BuildModel(workItem.docId, keyId, tokens);
                }
            }
        }

        private void BuildModel(long docId, long keyId, AnalyzedString tokens)
        {
            var ix = GetOrCreateIndex(keyId);

            foreach (var vector in tokens.Embeddings)
            {
                ix.Add(new VectorNode(vector, docId), CosineSimilarity.Term);
            }
        }

        public async Task Commit()
        {
            if (_committing || _committed)
                return;

            _committing = true;

            this.Log("waiting for model builder");

            using (_indexBuilder)
            {
                _indexBuilder.Join();
            }

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
                    var hit = tree.ClosestMatch(vector, CosineSimilarity.Term.foldAngle);

                    if (hit.Score < CosineSimilarity.Term.identicalAngle)
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