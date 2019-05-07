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
        private readonly ConcurrentDictionary<long, Stream> _vectorStreams;
        private readonly ConcurrentDictionary<long, long> _vectorStreamStartPositions;
        private bool _committed;
        private bool _committing;
        private readonly ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)> _indexBuilder;

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
            _vectorStreams = new ConcurrentDictionary<long, Stream>();
            _vectorStreamStartPositions = new ConcurrentDictionary<long, long>();

            var numThreads = int.Parse(_config.Get("write_thread_count"));

            _indexBuilder = new ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)>(
                numThreads, BuildModel);;
        }

        /// <summary>
        /// Fields prefixed with "___" or "__" will not be indexed.
        /// Fields prefixed with "_" will not be tokenized.
        /// </summary>
        public void Put(long docId, IDictionary doc)
        {
            foreach (var obj in doc.Keys)
            {
                var key = (string)obj;
                AnalyzedString tokens = null;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(CollectionId, keyHash);
                    var val = doc[key];
                    var str = val as string;

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

                    _indexBuilder.Enqueue((docId, keyId, tokens));
                }
            }
        }

        private void BuildModel((long docId, long keyId, AnalyzedString tokens) workItem)
        {
            var ix = GetOrCreateIndex(workItem.keyId);
            var vectorStream = GetOrCreateVectorStream(workItem.keyId);

            foreach (var vector in workItem.tokens.Embeddings)
            {
                ix.Add(new VectorNode(vector, workItem.docId), CosineSimilarity.Term, vectorStream);
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
                using (var writer = new ColumnSerializer(
                    CollectionId, column.Key, SessionFactory, new RemotePostingsWriter(_config, CollectionName)))
                {
                    var wt = writer.CreateColumnSegment(column.Value);

                    using (var vectorStream = _vectorStreams[column.Key])
                    {
                        vectorStream.Flush();
                    }

                    await wt;
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

        private Stream GetOrCreateVectorStream(long keyId)
        {
            return _vectorStreams.GetOrAdd(keyId, key =>
                {
                    var stream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{key}.vec"));

                    _vectorStreamStartPositions[keyId] = stream.Position;

                    return stream;
                }
            );
        }

        public void Dispose()
        {
        }
    }
}