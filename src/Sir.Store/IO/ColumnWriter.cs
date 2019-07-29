using System;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    public class ColumnWriter : ILogger, IDisposable
    {
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private static readonly object _indexFileSync = new object();
        private readonly PageIndexWriter _ixPageIndexWriter;
        private readonly Stream _ixStream;

        public ColumnWriter(
            ulong collectionId, 
            long keyId, 
            SessionFactory sessionFactory)
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;

            var pixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.ixp", _collectionId, keyId));
            var ixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.ix", _collectionId, keyId));

            _ixPageIndexWriter = new PageIndexWriter(_sessionFactory.CreateAppendStream(pixFileName));
            _ixStream = _sessionFactory.CreateAppendStream(ixFileName);
        }

        public void CreatePage(VectorNode column, Stream vectorStream, Stream postingsStream, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var page = GraphBuilder.SerializeTree(column, _ixStream, vectorStream, postingsStream, model);

            _ixPageIndexWriter.Write(page.offset, page.length);

            vectorStream.Flush();
            postingsStream.Flush();
            _ixStream.Flush();
            _ixPageIndexWriter.Flush();

            _sessionFactory.ClearPageInfo();

            var size = PathFinder.Size(column);

            this.Log($"serialized column {_keyId} in {time.Elapsed}. weight: {column.Weight} d: {size.depth} w: {size.width}");
        }

        public void Dispose()
        {
            _ixStream.Dispose();
            _ixPageIndexWriter.Dispose();
        }
    }
}