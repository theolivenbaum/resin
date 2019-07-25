using Sir.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ConcurrentDictionary<long, VectorNode> _index;
        private Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ProducerConsumerQueue<(long docId, long keyId, IVector term)> _workers;
        //private readonly ProducerConsumerQueue<(long keyId, VectorNode ix)> _serializer;
        private readonly object _stopTheWorld = new object();

        public TermIndexSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _model = model;
            _index = new ConcurrentDictionary<long, VectorNode>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(int.Parse(config.Get("index_session_thread_count")), Put);
            //_serializer = new ProducerConsumerQueue<(long keyId, VectorNode ix)>(1, CreatePage);
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens.Embeddings)
            {
                _workers.Enqueue((docId, keyId, vector));
            }
        }

        private void Put((long docId, long keyId, IVector term) workItem)
        {
            VectorNode ix = GetOrCreateIndex(workItem.keyId);

            //lock (_stopTheWorld)
            //{
            //    ix = GetOrCreateIndex(workItem.keyId);

            //    if (ix.Weight >= _model.PageWeight)
            //    {
            //        VectorNode page;

            //        _index.Remove(workItem.keyId, out page);

            //        _serializer.Enqueue((workItem.keyId, page));

            //        ix = GetOrCreateIndex(workItem.keyId);
            //    }
            //}

            var node = new VectorNode(workItem.term, workItem.docId);

            GraphBuilder.TryMergeConcurrent(ix, node, _model);
        }

        private VectorNode GetOrCreateIndex(long keyId)
        {
            return _index.GetOrAdd(keyId, new VectorNode());
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo(), _workers.Count);
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var node in _index)
            {
                yield return new GraphInfo(node.Key, node.Value);
            }
        }

        private void CreatePage((long keyId, VectorNode root) workItem)
        {
            using (var serializer = new ColumnWriter(CollectionId, workItem.keyId, SessionFactory))
            {
                serializer.CreatePage(workItem.root, _vectorStream, _postingsStream, _model);
            }

            SessionFactory.ClearPageInfo();
        }

        public void Dispose()
        {
            var timer = Stopwatch.StartNew();

            this.Log($"waiting for sync. queue length: {_workers.Count}");

            _workers.Dispose();
            //_serializer.Dispose();

            this.Log($"waited for sync for {timer.Elapsed}");

            foreach (var column in _index)
            {
                CreatePage((column.Key, column.Value));
            }

            _postingsStream.Dispose();
            _vectorStream.Dispose();
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
    }

    public class IndexInfo
    {
        public IEnumerable<GraphInfo> Info { get; }
        public int QueueLength { get; }

        public IndexInfo(IEnumerable<GraphInfo> info, int queueLength)
        {
            Info = info;
            QueueLength = queueLength;
        }
    }

    public class GraphInfo
    {
        private readonly long _keyId;
        private readonly VectorNode _graph;

        public GraphInfo(long keyId, VectorNode graph)
        {
            _keyId = keyId;
            _graph = graph;
        }

        public override string ToString()
        {
            return $"key {_keyId} | weight {_graph.Weight} {PathFinder.Size(_graph)}";
        }
    }
}