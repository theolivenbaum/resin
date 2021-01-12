using Sir.Documents;
using System;

namespace Sir.Search
{
    public class UpdateDocumentSession : IDisposable
    {
        private readonly DocumentReader _reader;
        private readonly DocumentWriter _writer;

        public UpdateDocumentSession(string directory, ulong collectionId, StreamFactory sessionFactory) 
        {
            _reader = new DocumentReader(directory, collectionId, sessionFactory);
            _writer = new DocumentWriter(directory, collectionId, sessionFactory);
        }

        public void Update(long docId, long keyId, object value)
        {
            var docAddress = _reader.GetDocumentAddress(docId);
            var docMap = _reader.GetDocumentMap(docAddress.offset, docAddress.length);
            long valueId = -1;

            foreach (var field in docMap)
            {
                if (field.keyId == keyId)
                {
                    valueId = field.valId;
                    break;
                }
            }

            if (valueId == -1)
                throw new Exception($"There was no field with keyId {keyId} in document {docId}.");

            var valueAddress = _reader.GetAddressOfValue(valueId);

            _writer.UpdateValue(valueAddress.offset, value);
        }

        public virtual void Dispose()
        {
            _writer.Dispose();
            _reader.Dispose();
        }
    }
}