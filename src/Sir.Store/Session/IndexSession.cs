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
    public class IndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>> _index;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ProducerConsumerQueue<(long docId, long keyId, IVector term)> _workers;
        private readonly ConcurrentDictionary<long, ConcurrentBag<IVector>> _debugWords;
        private readonly ConcurrentDictionary<long, long> _segmentId;
        private readonly string _fileExtension;

        public IndexSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config,
            string fileExtension) : base(collectionId, sessionFactory)
        {
            var threadCount = int.Parse(config.Get("index_session_thread_count"));

            this.Log($"{threadCount} threads");

            _config = config;
            _model = model;
            _index = new ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(threadCount, Put);
            _debugWords = new ConcurrentDictionary<long, ConcurrentBag<IVector>>();
            _segmentId = new ConcurrentDictionary<long, long>();
            _fileExtension = fileExtension;
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens.Embeddings)
            {
                _workers.Enqueue((docId, keyId, vector));
            }
        }

        private void Put((long docId, long keyId, IVector term) work)
        {
            var column = _index.GetOrAdd(work.keyId, new ConcurrentDictionary<long, VectorNode>());

            var ix0 = column.GetOrAdd(0, new VectorNode(0));

            long indexId1 = GraphBuilder.GetIdConcurrent(
                ix0, 
                new VectorNode(work.term, work.docId), 
                _model, 
                _model.FoldAngle0, 
                _model.IdenticalAngle0,
                () => _segmentId.AddOrUpdate(work.keyId, 1, (k, v) => ++v));

            var ix1 = column.GetOrAdd((int)indexId1, new VectorNode(1));

            var indexId2 = GraphBuilder.GetIdConcurrent(
                ix1,
                new VectorNode(work.term, work.docId),
                _model,
                _model.FoldAngle1,
                _model.IdenticalAngle1,
                () => _segmentId.AddOrUpdate(work.keyId, 1, (k, v) => ++v));

            var ix2 = column.GetOrAdd((int)indexId2, new VectorNode(2));

            GraphBuilder.TryMergeConcurrent(
                ix2, 
                new VectorNode(work.term, work.docId), 
                _model, 
                _model.FoldAngle, 
                _model.IdenticalAngle);
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo(), _workers.Count);
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var ix in _index)
            {
                foreach (var node in ix.Value)
                    yield return new GraphInfo(ix.Key, node.Key, node.Value);
            }
        }

        public void Dispose()
        {
            var timer = Stopwatch.StartNew();

            this.Log($"waiting for sync. queue length: {_workers.Count}");

            _workers.Dispose();

            this.Log($"awaited sync for {timer.Elapsed}");

            foreach (var column in _index)
            {
                if (column.Value.Count > 0)
                {
                    var ixFileName = Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.{_fileExtension}");

                    using (var indexStream = SessionFactory.CreateAppendStream(ixFileName))
                    using (var columnWriter = new ColumnWriter(CollectionId, column.Key, indexStream))
                    using (var segmentIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(
                        Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.{_fileExtension}p"))))
                    {
                        var offset = segmentIndexWriter.Offset;

                        var indices = new SortedList<long, VectorNode>(column.Value);

                        foreach (var ix in indices)
                        {
                            columnWriter.CreatePage(ix.Value, _vectorStream, _postingsStream, _model, segmentIndexWriter);
                        }

                        var length = segmentIndexWriter.Offset - offset;

                        using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(
                            Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixp"))))
                        {
                            pageIndexWriter.Put(offset, length);
                        }
                    }
                }                
            }

            SessionFactory.ClearPageInfo();

            _postingsStream.Dispose();
            _vectorStream.Dispose();

            if (_debugWords.Count > 0)
            {
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
        private readonly long _indexId;
        private readonly VectorNode _graph;

        public long Weight => _graph.Weight;

        public GraphInfo(long keyId, long indexId, VectorNode graph)
        {
            _keyId = keyId;
            _indexId = indexId;
            _graph = graph;
        }

        public override string ToString()
        {
            return $"key {_keyId} level {_graph.Level} indexId: {_indexId} weight {_graph.Weight} {PathFinder.Size(_graph)}";
        }
    }
}