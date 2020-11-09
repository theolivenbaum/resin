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

            ulong collectionId = _collectionId;

            Field collectionIdField;

            if (document.TryGetValue(SystemFields.CollectionId, out collectionIdField))
            {
                collectionId = (ulong)collectionIdField.Value;
            }

            Field sourceDocId;

            if (document.TryGetValue(SystemFields.DocumentId, out sourceDocId))
            {
                Write(SystemFields.DocumentId, (long)sourceDocId.Value, docMap);
            }

            Write(SystemFields.Created, DateTime.Now.ToBinary(), docMap);
            Write(SystemFields.CollectionId, collectionId, docMap);

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

    public class Document
    {
        public double Score { get; set; }
        public IList<Field> Fields { get; }

        public Document(IList<Field> fields)
        {
            Fields = fields;
        }

        public Field Get(string key)
        {
            foreach (var field in Fields)
            {
                if (field.Key == key)
                {
                    return field;
                }
            }

            throw new ArgumentException($"key {key} not found");
        }

        public bool TryGetValue(string key, out Field value)
        {
            foreach (var field in Fields)
            {
                if (field.Key == key)
                {
                    value = field;
                    return true;
                }
            }

            value = null;
            return false;
        }

        public void AddOrOverwrite(Field f)
        {
            bool found = false;

            foreach (var field in Fields)
            {
                if (field.Key == f.Key)
                {
                    field.Value = f.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Fields.Add(f);
            }
        }
    }

    public class Field
    {
        public long Id { get; set; }
        public string Key { get; }
        public object Value { get; set; }
        public bool Index { get; }
        public bool Store { get; }

        public Field(string key, object value, bool index = true, bool store = true)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Key = key;
            Value = value;
            Index = index;
            Store = store;
        }
    }
}