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
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly Stream _vectorStream;
        private bool _flushed;
        private bool _flushing;
        private readonly ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)> _vectorBuilder;
        private readonly ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)> _modelBuilder;

        public IndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId)));

            var numThreads = int.Parse(_config.Get("index_thread_count"));

            _vectorBuilder = new ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)>
                (CreateTermVector, int.Parse(config.Get("index_thread_count")));

            _modelBuilder = new ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)>
                (AddVectorToModel, int.Parse(config.Get("index_thread_count")));
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

            using (_vectorBuilder)
            {
                _vectorBuilder.Join();
            }

            using (_modelBuilder)
            {
                _modelBuilder.Join();
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
                        _vectorBuilder.Enqueue((docId, keyId, tokens));
                    }
                }
            }

            this.Log("analyzed document {0} ", docId);
        }

        private void CreateTermVector((long docId, long keyId, AnalyzedString tokens) item)
        {
            foreach (var termVector in item.tokens.Embeddings)
            {
                _modelBuilder.Enqueue((item.docId, item.keyId, termVector));
            }
        }

        private void AddVectorToModel((long docId, long keyId, SortedList<int, byte> vector) item)
        {
            var ix = GetOrCreateIndex(item.keyId);

            ix.Add(new VectorNode(item.vector, item.docId), VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle, _vectorStream);
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