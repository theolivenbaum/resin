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
        private bool _flushed;

        public IStringModel Model => _model;
        public ConcurrentDictionary<long, VectorNode> Index => _index;

        public IndexSession(
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
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens)
            {
                Put(docId, keyId, vector);
            }
        }

        public void Put(long docId, long keyId, IVector vector)
        {
            var column = _index.GetOrAdd(keyId, new VectorNode());

            GraphBuilder.MergeOrAdd(
                column,
                new VectorNode(vector, docId),
                _model,
                _model.FoldAngle,
                _model.IdenticalAngle);

            //var hit = PathFinder.ClosestMatch(column, vector, _model);

            //if (hit == null || hit.Score < _model.IdenticalAngle)
            //{
            //    throw new Exception();
            //}

            //if (!hit.Node.DocIds.Contains(docId))
            //{
            //    throw new ApplicationException();
            //}
        }

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo());
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var ix in _index)
            {
                yield return new GraphInfo(ix.Key, ix.Value);
            }
        }

        public void Flush()
        {
            if (_flushed)
                return;

            _flushed = true;

            var timer = Stopwatch.StartNew();

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
        }

        public void Dispose()
        {
            if (!_flushed)
                Flush();

            _postingsStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}