using System;
using System.IO;

namespace Sir.Store
{
    public class ColumnWriter : ILogger, IDisposable
    {
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private static readonly object _indexFileSync = new object();
        private readonly Stream _ixStream;

        public ColumnWriter(
            ulong collectionId, 
            long keyId,
            Stream indexStream)
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _ixStream = indexStream;
        }

        public void CreatePage(VectorNode column, Stream vectorStream, Stream postingsStream, IStringModel model, PageIndexWriter pageIndexWriter)
        {
            var page = GraphBuilder.SerializeTree(column, _ixStream, vectorStream, postingsStream, model);

            pageIndexWriter.Put(page.offset, page.length);

            var size = PathFinder.Size(column);

            this.Log($"serialized column {_keyId} level {column.Level} weight: {column.Weight} {size}");
        }

        public void Dispose()
        {
            _ixStream.Dispose();
        }
    }
}