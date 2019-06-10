using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class ColumnSerializer : ILogger, IDisposable
    {
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private static readonly object _indexFileSync = new object();
        private readonly PageIndexWriter _ixPageIndexWriter;
        private readonly Stream _ixStream;

        public ColumnSerializer(
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

        public void CreateColumnSegment(VectorNode column, Stream vectorStream, Stream postingsStream, IStringModel model)
        {
            var time = Stopwatch.StartNew();

            var page = GraphBuilder.SerializeTree(column, _ixStream, vectorStream, postingsStream, model);

            _ixStream.Flush();
            _ixPageIndexWriter.Write(page.offset, page.length);
            _ixPageIndexWriter.Flush();

            var size = PathFinder.Size(column);

            this.Log("serialized column {0} in {1}. weight {2} depth {3} width {4} (avg depth {5})",
                _keyId, time.Elapsed, column.Weight, size.depth, size.width, size.avgDepth);
        }

        public void Dispose()
        {
            _ixStream.Dispose();
            _ixPageIndexWriter.Dispose();
        }
    }
}