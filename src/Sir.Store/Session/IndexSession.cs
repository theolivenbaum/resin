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
    public class IndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private bool _validate;
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly Stream _vectorStream;
        private bool _flushed;
        private bool _flushing;
        private readonly ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)> _modelBuilder;
        private readonly ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)> _modelBuilder2;

        public IndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _validate = config.Get("validate_when_indexing") == "true";
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId)));

            var numThreads = int.Parse(_config.Get("index_thread_count"));

            _modelBuilder = new ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)>
                (AddDocumentToModel, 8);

            _modelBuilder2 = new ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)>
                (AddDocumentToModel2, 8);
        }

        public void EmbedTerms(IDictionary document)
        {
            Analyze(document);
        }

        public void Flush()
        {
            if (_flushing || _flushed)
                return;

            _flushing = true;

            this.Log("waiting for model builder");

            using (_modelBuilder)
            {
                _modelBuilder.Join();
            }

            using (_modelBuilder2)
            {
                _modelBuilder2.Join();
            }

            var tasks = new Task[_dirty.Count];
            var taskId = 0;
            var columnWriters = new List<ColumnSerializer>();

            foreach (var column in _dirty)
            {
                var columnWriter = new ColumnSerializer(CollectionId, column.Key, SessionFactory, new RemotePostingsWriter(_config));
                columnWriters.Add(columnWriter);
                tasks[taskId++] = columnWriter.SerializeColumnSegment(column.Value);
            }

            using (_vectorStream)
            {
                _vectorStream.Flush();
                _vectorStream.Close();
            }

            Task.WaitAll(tasks);

            foreach (var writer in columnWriters)
            {
                writer.Dispose();
            }

            _flushed = true;
            _flushing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private bool Validate(long keyId, AnalyzedString tokens)
        {
            var tree = _dirty[keyId];

            foreach (var vector in tokens.Embeddings)
            {
                var hit = tree.ClosestMatch(vector);

                if (hit.Score < VectorNode.IdenticalTermAngle)
                {
                    throw new DataMisalignedException();
                }
            }

            return true;
        }

        private void Analyze(IDictionary document)
        {
            var docId = (long)document["__docid"];

            foreach (var obj in document.Keys)
            {
                var key = (string)obj;
                AnalyzedString tokens = null;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(keyHash);
                    var val = (IComparable)document[key];
                    var str = val as string;

                    if (str == null || key[0] == '_')
                    {
                        var v = val.ToString();

                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            tokens = new AnalyzedString { Source = v.ToCharArray(), Tokens = new List<(int, int)> { (0, v.Length) } };
                        }
                    }
                    else
                    {
                        tokens = _tokenizer.Tokenize(str);
                    }

                    if (tokens != null)
                    {
                        _modelBuilder.Enqueue((docId, keyId, tokens));
                    }
                }
            }

            if (!_validate)
                this.Log("analyzed document ID {0}", docId);
        }

        private void AddDocumentToModel((long docId, long keyId, AnalyzedString tokens) item)
        {
            foreach (var termVector in item.tokens.Embeddings)
            {
                _modelBuilder2.Enqueue((item.docId, item.keyId, termVector));
            }

            if (_validate)
            {
                Validate(item.keyId, item.tokens);

                this.Log("validated doc {0}", item.docId);
            }
        }

        private void AddDocumentToModel2((long docId, long keyId, SortedList<int, byte> vector) item)
        {
            var ix = GetOrCreateIndex(item.keyId);

            ix.Add(new VectorNode(item.vector, item.docId), VectorNode.IdenticalTermAngle, VectorNode.TermFoldAngle, _vectorStream);
        }

        private static readonly object _syncIndexAccess = new object();

        private VectorNode GetOrCreateIndex(long keyId)
        {
            VectorNode root;

            if (!_dirty.TryGetValue(keyId, out root))
            {
                lock (_syncIndexAccess)
                {
                    if (!_dirty.TryGetValue(keyId, out root))
                    {
                        root = new VectorNode();
                        _dirty.Add(keyId, root);
                    }
                }
            }

            return root;
        }

        public void Dispose()
        {
            Flush();
        }
    }
}