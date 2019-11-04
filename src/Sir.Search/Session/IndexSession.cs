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
    public class IndexSession : IDisposable, ILogger
    {
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly Stream _postingsStream;
        private readonly Stream _vectorStream;
        private bool _flushed;

        public IStringModel Model { get; }
        public ConcurrentDictionary<long, VectorNode> Index { get; }

        public IndexSession(
            ulong collectionId,
            SessionFactory sessionFactory,
            IStringModel model,
            IConfigurationProvider config)
        {
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _config = config;
            _postingsStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, $"{collectionId}.pos"));
            _vectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, $"{collectionId}.vec"));
            
            Model = model;
            Index = new ConcurrentDictionary<long, VectorNode>();
        }

        public void Put(long docId, long keyId, string value)
        {
            var tokens = Model.Tokenize(value);

            foreach (var vector in tokens)
            {
                Put(docId, keyId, vector);
            }
        }

        public void Put(long docId, long keyId, IVector vector)
        {
            var column = Index.GetOrAdd(keyId, new VectorNode());

            GraphBuilder.MergeOrAdd(
                column,
                new VectorNode(vector, docId),
                Model,
                Model.FoldAngle,
                Model.IdenticalAngle);

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
            foreach (var ix in Index)
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

            foreach (var column in Index)
            {
                var ixFileName = Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{column.Key}.ix");

                using (var indexStream = _sessionFactory.CreateAppendStream(ixFileName))
                using (var columnWriter = new ColumnWriter(_collectionId, column.Key, indexStream))
                using (var pageIndexWriter = new PageIndexWriter(
                    _sessionFactory.CreateAppendStream(
                        Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{column.Key}.ixtp"))))
                {
                    columnWriter.CreatePage(column.Value, _vectorStream, _postingsStream, pageIndexWriter);
                }
            }

            _sessionFactory.ClearPageInfo();
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