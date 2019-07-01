using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    public class DocumentStreamSession : CollectionSession, IDisposable
    {
        private readonly CollectionStreamReader _streamReader;

        public DocumentStreamSession(string collectionName, ulong collectionId, SessionFactory sessionFactory, CollectionStreamReader streamReader) 
            : base(collectionName, collectionId, sessionFactory)
        {
            _streamReader = streamReader;
        }

        public void Dispose()
        {
            _streamReader.Dispose();
        }

        public IEnumerable<IDictionary> ReadDocs(int skip = 0, int take = 0)
        {
            return ReadDocs().Skip(skip).Take(take);
        }

        public void Index(TermIndexSession indexSession, TextWriter log)
        {
            var docId = 0;
            var docCount = _streamReader.DocumentCount();
            var timer = new Stopwatch();

            while (docId < docCount)
            {
                timer.Restart();

                var docInfo = _streamReader.GetDocumentAddress(docId);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _streamReader.GetDocumentMap(docInfo.offset, docInfo.length);

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _streamReader.GetAddressOfKey(kvp.keyId);
                    var key = _streamReader.GetKey(kInfo.offset, kInfo.len, kInfo.dataType);

                    var strKey = (string)key;

                    if (strKey.StartsWith("_"))
                        continue;

                    var vInfo = _streamReader.GetAddressOfValue(kvp.valId);
                    var val = (string)_streamReader.GetValue(vInfo.offset, vInfo.len, vInfo.dataType);

                    indexSession.Put(docId, kvp.keyId, val);
                }

                log.WriteLine(docId);

                docId++;
            }
        }

        public IEnumerable<IDictionary> ReadDocs()
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
