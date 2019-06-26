using RocksDbSharp;
using System;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    public class ColumnSerializer : ILogger, IDisposable
    {
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly SessionFactory _sessionFactory;
        private readonly RocksDb _db;
        private readonly ColumnFamilyHandle _cf;
        private readonly PageIndexWriter _ixPageIndexWriter;

        public ColumnSerializer(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            RocksDb db)
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;

            var ixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.ix", _collectionId, keyId));
            var pixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.ixp", _collectionId, keyId));

            _ixPageIndexWriter = new PageIndexWriter(_sessionFactory.CreateAppendStream(pixFileName));

            _db = db;

            _cf = db.GetColumnFamily(ixFileName);

            if (_cf == null || _cf.Handle == null)
            {
                _cf = db.CreateColumnFamily(new ColumnFamilyOptions(), ixFileName);
            }
        }

        public void CreateColumnSegment(
            VectorNode column, Stream vectorStream, Stream postingsStream, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var page = GraphBuilder.SerializeTree(column, _db, _cf, vectorStream, postingsStream, model);

            _ixPageIndexWriter.Write(page.offset, page.length);

            var size = PathFinder.Size(column);

            this.Log("serialized column {0} in {1}. weight {2} depth {3} width {4} (avg depth {5})",
                _keyId, time.Elapsed, column.Weight, size.depth, size.width, size.avgDepth);
        }

        public void Dispose()
        {
            _ixPageIndexWriter.Dispose();
        }
    }
}