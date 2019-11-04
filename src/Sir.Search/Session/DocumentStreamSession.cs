using Sir.Document;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    public class DocumentStreamSession : IDisposable
    {
        private readonly DocumentReader _streamReader;

        public DocumentStreamSession(DocumentReader streamReader) 
        {
            _streamReader = streamReader;
        }

        public void Dispose()
        {
            _streamReader.Dispose();
        }

        public IEnumerable<IDictionary> ReadDocs(ulong collectionId, int skip = 0, int take = 0)
        {
            return ReadDocs(collectionId).Skip(skip).Take(take);
        }

        public IEnumerable<IDictionary> ReadDocs(ulong collectionId)
        {
            long docId = 1;
            var docCount = _streamReader.DocumentCount();

            while (docId < docCount)
            {
                var docInfo = _streamReader.GetDocumentAddress(docId);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _streamReader.GetDocumentMap(docInfo.offset, docInfo.length);
                var doc = new Dictionary<object, object>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _streamReader.GetAddressOfKey(kvp.keyId);
                    var vInfo = _streamReader.GetAddressOfValue(kvp.valId);
                    var key = _streamReader.GetKey(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = _streamReader.GetValue(vInfo.offset, vInfo.len, vInfo.dataType);

                    doc[key] = val;
                }

                doc["___docid"] = docId++;

                yield return doc;
            }
        }
    }
}
