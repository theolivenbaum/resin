using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class CollectionStreamWriter : IDisposable
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocMapWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;

        public CollectionStreamWriter(ulong collectionId, SessionFactory sessionFactory)
        {
            var valueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)), int.Parse(sessionFactory.Config.Get("value_stream_buffer_size")));
            var keyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            var docStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)), int.Parse(sessionFactory.Config.Get("doc_map_stream_buffer_size")));
            var valueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            var keyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            var docIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));

            _vals = new ValueWriter(valueStream);
            _keys = new ValueWriter(keyStream);
            _docs = new DocMapWriter(docStream);
            _valIx = new ValueIndexWriter(valueIndexStream);
            _keyIx = new ValueIndexWriter(keyIndexStream);
            _docIx = new DocIndexWriter(docIndexStream);
        }

        public long GetNextDocId()
        {
            return _docIx.GetNextDocId();
        }

        public (long offset, int len, byte dataType) PutKey(object value)
        {
            return _keys.Append(value);
        }

        public (long offset, int len, byte dataType) PutValue(object value)
        {
            return _vals.Append(value);
        }

        public long PutKeyInfo(long offset, int len, byte dataType)
        {
            return _keyIx.Append(offset, len, dataType);
        }

        public long PutValueInfo(long offset, int len, byte dataType)
        {
            return _valIx.Append(offset, len, dataType);
        }

        public (long offset, int length) PutDocumentMap(IList<(long keyId, long valId)> doc)
        {
            return _docs.Append(doc);
        }

        public void PutDocumentAddress(long offset, int len)
        {
            _docIx.Append(offset, len);
        }

        public void Flush()
        {
            _vals.Flush();
            _keys.Flush();
            _docs.Flush();
            _valIx.Flush();
            _keyIx.Flush();
            _docIx.Flush();
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
