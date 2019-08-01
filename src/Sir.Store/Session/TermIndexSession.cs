using Sir.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, VectorNode> _index1;
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>> _index2;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ProducerConsumerQueue<(long docId, long keyId, IVector term)> _workers;
        private readonly ConcurrentDictionary<long, ConcurrentBag<IVector>> _debugWords;

        public TermIndexSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            var threadCount = int.Parse(config.Get("index_session_thread_count"));

            this.Log($"{threadCount} threads");

            _config = config;
            _model = model;
            _index1 = new ConcurrentDictionary<long, VectorNode>();
            _index2 = new ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(threadCount, Put);
            _debugWords = new ConcurrentDictionary<long, ConcurrentBag<IVector>>();
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
            VectorNode ix1 = _index1.GetOrAdd(workItem.keyId, new VectorNode());
            long indexId;

            GraphBuilder.TryMergeConcurrent(
                ix1, new VectorNode(workItem.term, workItem.docId), _model, _model.PrimaryFoldAngle, _model.PrimaryIdenticalAngle, out indexId);

            var ix2 = _index2
                    .GetOrAdd(workItem.keyId, new ConcurrentDictionary<long, VectorNode>())
                    .GetOrAdd(indexId, new VectorNode());

            GraphBuilder.TryMergeConcurrent(
                ix2, new VectorNode(workItem.term, workItem.docId), _model, _model.FoldAngle, _model.IdenticalAngle);
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo(), _workers.Count);
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var node in _index1)
            {
                yield return new GraphInfo(node.Key, node.Value);
            }
        }

        public void Dispose()
        {
            var timer = Stopwatch.StartNew();

            this.Log($"waiting for sync. queue length: {_workers.Count}");

            _workers.Dispose();

            this.Log($"awaited sync for {timer.Elapsed}");

            foreach (var column in _index1)
            {
                var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, column.Key));

                using (var indexStream = SessionFactory.CreateAppendStream(ixFileName))
                using (var columnWriter = new ColumnWriter(CollectionId, column.Key, indexStream))
                {
                    using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(
                        Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixp1"))))
                    {
                        columnWriter.CreatePage(column.Value, _vectorStream, _postingsStream, _model, pageIndexWriter);
                    }

                    var indices = new SortedList<long, VectorNode>(_index2[column.Key]);

                    using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(
                        Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixp2"))))
                    {
                        foreach (var ix in indices)
                        {
                            columnWriter.CreatePage(ix.Value, _vectorStream, _postingsStream, _model, pageIndexWriter);
                        }
                    }
                }
            }

            SessionFactory.ClearPageInfo();

            _postingsStream.Dispose();
            _vectorStream.Dispose();

            var debugOutput = new StringBuilder();

            foreach (var key in _debugWords)
            {
                var sorted = new SortedList<string, object>();

                foreach (var word in key.Value)
                {
                    sorted.Add(word.ToString(), null);
                }

                debugOutput.AppendLine($"{key.Key}: {sorted.Count} words");

                foreach (var word in sorted)
                {
                    debugOutput.AppendLine($"{key.Key} {word.Key}");
                }
            }

            this.Log(debugOutput);
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