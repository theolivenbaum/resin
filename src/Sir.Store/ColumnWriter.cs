using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class ColumnWriter : IDisposable, ILogger
    {
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly RemotePostingsWriter _postingsWriter;
        private readonly SessionFactory _sessionFactory;
        private static readonly object _indexFileSync = new object();
        private readonly PageIndexWriter _pageIndexWriter;
        private readonly Stream _ixStream;

        public ColumnWriter(ulong collectionId, long keyId, SessionFactory sessionFactory, RemotePostingsWriter postingsWriter = null, string fileExtension = "ix")
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _postingsWriter = postingsWriter;
            _sessionFactory = sessionFactory;

            var pixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.{2}p", _collectionId, keyId, fileExtension));
            var ixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.{2}", _collectionId, keyId, fileExtension));

            _pageIndexWriter = new PageIndexWriter(_sessionFactory.CreateAppendStream(pixFileName));
            _ixStream = _sessionFactory.CreateAppendStream(ixFileName);
        }

        public async Task WriteColumnSegment(VectorNode column)
        {
            var time = Stopwatch.StartNew();

            (int depth, int width, int avgDepth) size;

            if (_postingsWriter != null)
            {
                await _postingsWriter.Write(_collectionId, column);
            }

            lock (_indexFileSync)
            {
                var page = column.SerializeTree(_ixStream);

                _pageIndexWriter.Write(page.offset, page.length);
            }

            size = column.Size();

            this.Log("serialized column {0} in {1} with size {2},{3} (avg depth {4})",
                _keyId, time.Elapsed, size.depth, size.width, size.avgDepth);
        }

        public void Dispose()
        {
            _ixStream.Dispose();
            _pageIndexWriter.Dispose();
        }
    }
}