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
        private readonly ConcurrentDictionary<long, VectorNode> _index;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private readonly ProducerConsumerQueue<(long docId, long keyId, IVector term)> _workers;

        public IStringModel Model => _model;
        public ConcurrentDictionary<long, VectorNode> Index => _index;

        public IndexSession(
            ulong collectionId,
            SessionFactory sessionFactory,
            IStringModel model,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            var threadCount = int.Parse(config.Get("index_session_thread_count"));

            this.Log($"starting {threadCount} threads");

            _config = config;
            _model = model;
            _index = new ConcurrentDictionary<long, VectorNode>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(long docId, long keyId, IVector term)>(threadCount, Put);
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
            var column = _index.GetOrAdd(work.keyId, new VectorNode(0));

            GraphBuilder.TryMergeConcurrent(
                column,
                new VectorNode(work.term, work.docId),
                _model,
                _model.FoldAngle,
                _model.IdenticalAngle);
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var ix in _index)
            {
                yield return new GraphInfo(ix.Key, ix.Value);
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
                var ixFileName = Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ix");

                using (var indexStream = SessionFactory.CreateAppendStream(ixFileName))
                using (var columnWriter = new ColumnWriter(CollectionId, column.Key, indexStream))
                using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{column.Key}.ixtp"))))
                {
                    columnWriter.CreatePage(column.Value, _vectorStream, _postingsStream, pageIndexWriter);
                }
            }

            SessionFactory.ClearPageInfo();

            _postingsStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}