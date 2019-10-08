using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.KeyValue
{
    public class DocumentReader : IDisposable
    {
        private readonly ValueReader _vals;
        private readonly ValueReader _keys;
        private readonly DocMapReader _docs;
        private readonly ValueIndexReader _valIx;
        private readonly ValueIndexReader _keyIx;
        private readonly DocIndexReader _docIx;

        public DocumentReader(ulong collectionId, ISessionFactory sessionFactory)
        {
            var valueStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)), int.Parse(sessionFactory.Config.Get("value_stream_read_buffer_size")));
            var keyStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            var docStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)), int.Parse(sessionFactory.Config.Get("doc_map_stream_read_buffer_size")));
            var valueIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            var keyIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            var docIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));

            _vals = new ValueReader(valueStream);
            _keys = new ValueReader(keyStream);
            _docs = new DocMapReader(docStream);
            _valIx = new ValueIndexReader(valueIndexStream);
            _keyIx = new ValueIndexReader(keyIndexStream);
            _docIx = new DocIndexReader(docIndexStream);
        }

        public (long offset, int length) GetDocumentAddress(long docId)
        {
            return _docIx.Get(docId);
        }

        public IList<(long keyId, long valId)> GetDocumentMap(long offset, int length)
        {
            return _docs.Get(offset, length);
        }

        public (long offset, int len, byte dataType) GetAddressOfKey(long id)
        {
            return _keyIx.Get(id);
        }

        public (long offset, int len, byte dataType) GetAddressOfValue(long id)
        {
            return _valIx.Get(id);
        }

        public object GetKey(long offset, int len, byte dataType)
        {
            return _keys.Get(offset, len, dataType);
        }

        public object GetValue(long offset, int len, byte dataType)
        {
            return _vals.Get(offset, len, dataType);
        }

        public int DocumentCount()
        {
            return _docIx.Count;
        }

        public void Dispose()
        {
            _vals.Dispose();
            _keys.Dispose();
            _docs.Dispose();
            _valIx.Dispose();
            _keyIx.Dispose();
            _docIx.Dispose();
        }
    }
}
