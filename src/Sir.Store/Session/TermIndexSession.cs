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
        private readonly ConcurrentDictionary<long, ProducerConsumerQueue<(long docId, long keyId, IVector term)>> _workers;

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
            _workers = new ConcurrentDictionary<long, ProducerConsumerQueue<(long docId, long keyId, IVector term)>>();
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = _model.Tokenize(value);
            var worker = GetOrCreateWorker(keyId);

            foreach (var vector in tokens.Embeddings)
            {
                worker.Enqueue((docId, keyId, vector));
            }
        }

        private void Put((long docId, long keyId, IVector term) workItem)
        {
            var ix = GetOrCreateIndex(workItem.keyId);
            var node = new VectorNode(workItem.term, workItem.docId);

            VectorNode vertex;

            if (GraphBuilder.TryMerge(ix, node, _model, out vertex))
            {
                GraphBuilder.AddDocId(vertex, workItem.docId);
            }
        }

        private ProducerConsumerQueue<(long, long, IVector)> GetOrCreateWorker(long keyId)
        {
            return _workers.GetOrAdd(keyId, new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(1, Put));
        }

        private VectorNode GetOrCreateIndex(long keyId)
        {
            return _index.GetOrAdd(keyId, new VectorNode());
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo());
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var node in _index)
            {
                yield return new GraphInfo(node.Key, node.Value, _workers[node.Key].Count);
            }
        }

        private void Serialize(long keyId)
        {
            using (var serializer = new ColumnWriter(CollectionId, keyId, SessionFactory))
            {
                serializer.CreatePage(_index[keyId], _vectorStream, _postingsStream, _model);
            }

            SessionFactory.ClearPageInfo();
        }

        public void Dispose()
        {
            var timer = Stopwatch.StartNew();

            this.Log("waiting for sync");

            foreach (var worker in _workers)
                worker.Value.CompleteAdding();

            foreach (var worker in _workers)
                worker.Value.Dispose();

            this.Log($"waited for sync for {timer.Elapsed}");

            foreach (var column in _index.Keys)
            {
                Serialize(column);
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

        public IndexInfo(IEnumerable<GraphInfo> info)
        {
            Info = info;
        }
    }

    public class GraphInfo
    {
        private readonly long _keyId;
        private readonly VectorNode _graph;
        private readonly int _queueLength;

        public GraphInfo(long keyId, VectorNode graph, int queueLength)
        {
            _keyId = keyId;
            _graph = graph;
            _queueLength = queueLength;
        }

        public override string ToString()
        {
            return $"key {_keyId} | weight {_graph.Weight} | depth/width {PathFinder.Size(_graph)} | queue length {_queueLength}";
        }
    }
}