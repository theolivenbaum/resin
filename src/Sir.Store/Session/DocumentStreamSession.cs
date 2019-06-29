using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    public class DocumentStreamSession : DocumentSession
    {
        private readonly DocIndexReader _docIx;
        private readonly DocMapReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;

        public DocumentStreamSession(string collectionName, ulong collectionId, SessionFactory sessionFactory) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _docIx = new DocIndexReader(Stream.Synchronized(DocIndexStream));
            _docs = new DocMapReader(Stream.Synchronized(DocStream));
            _keyIx = new ValueIndexReader(Stream.Synchronized(KeyIndexStream));
            _valIx = new ValueIndexReader(Stream.Synchronized(ValueIndexStream));
            _keyReader = new ValueReader(Stream.Synchronized(KeyStream));
            _valReader = new ValueReader(Stream.Synchronized(ValueStream));
        }

        public IEnumerable<IDictionary> ReadDocs(int skip = 0, int take = 0)
        {
            return ReadDocs().Skip(skip).Take(take);
        }

        public void Index(TermIndexSession indexSession, TextWriter log)
        {
            var docId = 0;
            var docCount = _docIx.Count;
            var timer = new Stopwatch();

            while (docId < docCount)
            {
                timer.Restart();

                var docInfo = _docIx.Read(docId);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _docs.Read(docInfo.offset, docInfo.length);

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _keyIx.Read(kvp.keyId);
                    var key = _keyReader.Read(kInfo.offset, kInfo.len, kInfo.dataType);

                    var strKey = (string)key;

                    if (strKey.StartsWith("_"))
                        continue;

                    var vInfo = _valIx.Read(kvp.valId);
                    var val = (string)_valReader.Read(vInfo.offset, vInfo.len, vInfo.dataType);

                    indexSession.Put(docId, kvp.keyId, val);
                }

                log.WriteLine(docId);

                docId++;
            }
        }

        public IEnumerable<IDictionary> ReadDocs()
        {
            var docId = 0;
            var docCount = _docIx.Count;

            while (docId < docCount)
            {
                var docInfo = _docIx.Read(docId);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _docs.Read(docInfo.offset, docInfo.length);
                var doc = new Dictionary<object, object>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _keyIx.Read(kvp.keyId);
                    var vInfo = _valIx.Read(kvp.valId);
                    var key = _keyReader.Read(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = _valReader.Read(vInfo.offset, vInfo.len, vInfo.dataType);

                    doc[key] = val;
                }

                doc["___docid"] = docId;

                yield return doc;
            }
        }
    }
}
