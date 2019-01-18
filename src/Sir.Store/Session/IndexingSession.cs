using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession, IDisposable
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private bool _validate;
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly Stream _vectorStream;
        private bool _flushed;
        private bool _flushing;
        private readonly ProducerConsumerQueue<IDictionary> _analyzeQueue;
        private readonly RemotePostingsWriter _postingsWriter;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId.ToHash())));
            _postingsWriter = new RemotePostingsWriter(_config);

            var numThreads = int.Parse(_config.Get("index_thread_count"));

            _analyzeQueue = new ProducerConsumerQueue<IDictionary>(Analyze, numThreads);
        }

        public void Write(IDictionary document)
        {
            _analyzeQueue.Enqueue(document);
        }

        public void Flush()
        {
            if (_flushing || _flushed)
                return;

            _flushing = true;

            Logging.Log("waiting for analyze queue");

            using (_analyzeQueue)
            {
                _analyzeQueue.Join();
            }

            var tasks = new Task[_dirty.Count];
            var taskId = 0;

            foreach (var column in _dirty)
            {
                tasks[taskId++] = SerializeColumn(column.Key, column.Value);
            }

            using (_vectorStream)
            {
                _vectorStream.Flush();
                _vectorStream.Close();
            }

            Task.WaitAll(tasks);

            _flushed = true;
            _flushing = false;

            Logging.Log(string.Format("***FLUSHED***"));
        }

        private async Task SerializeColumn(long keyId, VectorNode column)
        {
            var time = Stopwatch.StartNew();
            (int depth, int width, int avgDepth) size;

            var collectionId = CollectionId.ToHash();

            await _postingsWriter.Write(collectionId, column);

            var pixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));

            using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(pixFileName)))
            using (var ixStream = CreateIndexStream(collectionId, keyId))
            {
                var page = column.SerializeTree(ixStream);

                pageIndexWriter.Write(page.offset, page.length);

                size = column.Size();
            }

            Logging.Log("serialized column {0} in {1} with size {2},{3} (avg depth {4})",
                keyId, time.Elapsed, size.depth, size.width, size.avgDepth);
        }

        private void Analyze(IDictionary doc)
        {
            var time = Stopwatch.StartNew();

            var docId = (ulong)doc["__docid"];

            foreach (var obj in doc.Keys)
            {
                var key = (string)obj;
                AnalyzedString tokens = null;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(keyHash);
                    var val = (IComparable)doc[key];
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
                        AddDocumentToModel(docId, keyId, tokens);
                    }
                }
            }

            Logging.Log("added document ID {0} to model in {1}", docId, time.Elapsed);
        }

        private void AddDocumentToModel(ulong docId, long keyId, AnalyzedString tokens)
        {
            var ix = GetOrCreateIndex(keyId);

            foreach (var token in tokens.Tokens)
            {
                var termVector = tokens.ToCharVector(token.offset, token.length);

                ix.Add(new VectorNode(termVector, docId), _vectorStream);
            }
        }

        private Stream CreateIndexStream(ulong collectionId, long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            return SessionFactory.CreateAppendStream(fileName);
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
            while (_flushing)
            {
                Thread.Sleep(100);
            }
        }
    }
}