using Sir.Document;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : IDisposable
    {
        private readonly ulong _collectionId;
        private readonly DocumentWriter _streamWriter;
        private readonly SessionFactory _sessionFactory;

        public WriteSession(
            ulong collectionId,
            DocumentWriter streamWriter,
            SessionFactory sessionFactory)
        {
            _collectionId = collectionId;
            _streamWriter = streamWriter;
            _sessionFactory = sessionFactory;
        }

        public long Put(Document document)
        {
            var docMap = new List<(long keyId, long valId)>();

            foreach (var field in document.Fields)
            {
                if (field.Value != null && field.Store)
                {
                    Write(field, docMap);
                }
                else
                {
                    continue;
                }
            }

            if (!document.TryGetValue(SystemFields.CollectionId, out _))
            {
                Write(SystemFields.CollectionId, _collectionId, docMap);
            }

            Write(SystemFields.Created, DateTime.Now.ToBinary(), docMap);

            var docMeta = _streamWriter.PutDocumentMap(docMap);
            var docId = _streamWriter.IncrementDocId();

            _streamWriter.PutDocumentAddress(docId, docMeta.offset, docMeta.length);
            
            return docId;
        }

        private void Write(Field field, IList<(long, long)> docMap)
        {
            var keyId = EnsureKeyExists(field.Key);

            Write(keyId, field.Value, docMap);

            field.Id = keyId;
        }

        private void Write(string key, object val, IList<(long, long)> docMap)
        {
            var keyId = EnsureKeyExists(key);

            Write(keyId, val, docMap);
        }

        private void Write(long keyId, object val, IList<(long, long)> docMap)
        {
            // store k/v
            var kvmap = _streamWriter.Put(keyId, val, out _);

            // store refs to k/v pair
            docMap.Add(kvmap);
        }

        public long EnsureKeyExists(string key)
        {
            return _streamWriter.EnsureKeyExists(key);
        }

        public long EnsureKeyExistsSafely(string key)
        {
            return _streamWriter.EnsureKeyExistsSafely(key);
        }

        public void Dispose()
        {
            _streamWriter.Dispose();
        }
    }
}