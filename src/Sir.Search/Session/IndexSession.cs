using Sir.Core;
using Sir.VectorSpace;
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
    public class IndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>> _index;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ProducerConsumerQueue<(long docId, long keyId, IVector term)> _workers;
        private readonly ConcurrentDictionary<long, long> _segmentId;

        public IStringModel Model => _model;
        public ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>> Index => _index;

        public IndexSession(
            ulong collectionId,
            SessionFactory sessionFactory,
            IStringModel model,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            var threadCount = int.Parse(config.Get("index_session_thread_count"));

            this.Log($"{threadCount} threads");

            _config = config;
            _model = model;
            _index = new ConcurrentDictionary<long, ConcurrentDictionary<long, VectorNode>>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(threadCount, Put);
            _segmentId = new ConcurrentDictionary<long, long>();
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens.Embeddings)
            {
                _workers.Enqueue((docId, keyId, vector));
            }
        }

        public void Enqueue(long docId, long keyId, IVector term)
        {
            _workers.Enqueue((docId, keyId, term));
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo(), _workers.Count);
        }

        private void Put((long docId, long keyId, IVector term) work)
        {
            var column = _index.GetOrAdd(work.keyId, new ConcurrentDictionary<long, VectorNode>());

            var ix0 = column.GetOrAdd(0, new VectorNode(0));

            long indexId1 = GraphBuilder.GetOrIncrementIdConcurrent(
                ix0,
                new VectorNode(work.term, work.docId),
                _model,
                _model.FoldAngleFirst,
                _model.IdenticalAngleFirst,
                () => _segmentId.AddOrUpdate(work.keyId, 1, (k, v) => ++v));

            var ix1 = column.GetOrAdd((int)indexId1, new VectorNode(1));

            var indexId2 = GraphBuilder.GetOrIncrementIdConcurrent(
                ix1,
                new VectorNode(work.term, work.docId),
                _model,
                _model.FoldAngleSecond,
                _model.IdenticalAngleSecond,
                () => _segmentId.AddOrUpdate(work.keyId, 1, (k, v) => ++v));

            var ix2 = column.GetOrAdd((int)indexId2, new VectorNode(2));

            GraphBuilder.TryMergeConcurrent(
                ix2,
                new VectorNode(work.term, work.docId),
                _model,
                _model.FoldAngle,
                _model.IdenticalAngle);
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
                    var ixFileName = Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ix");

                    using (var indexStream = SessionFactory.CreateAppendStream(ixFileName))
                    using (var columnWriter = new ColumnWriter(CollectionId, column.Key, indexStream))
                    using (var segmentIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixtsp"))))
                    using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixtp"))))
                    {
                        var offset = segmentIndexWriter.Position;
                        var indices = new SortedList<long, VectorNode>(column.Value);

                        foreach (var ix in indices)
                        {
                            columnWriter.CreatePage(ix.Value, _vectorStream, _postingsStream, segmentIndexWriter);
                        }

                        var length = segmentIndexWriter.Position - offset;

                        pageIndexWriter.Put(offset, length);
                    }
                }
            }

            SessionFactory.ClearPageInfo();

            _postingsStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}