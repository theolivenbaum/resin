using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    public class DocumentReadSession : CollectionSession
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;

        public DocumentReadSession(string collectionId, LocalStorageSessionFactory sessionFactory) 
            : base(collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
        }

        public IEnumerable<IDictionary> ReadDocs()
        {
            var numOfDocs = _docIx.NumOfDocs;
            var docIds = Enumerable.Range(1, numOfDocs).ToDictionary(x => (ulong)x, y => (float)0);

            return ReadDocs(docIds);
        }

        public IEnumerable<IDictionary> ReadDocs(IDictionary<ulong, float> docs)
        {
            foreach (var d in docs)
            {
                var docInfo = _docIx.Read(d.Key);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _docs.Read(docInfo.offset, docInfo.length);
                var doc = new Dictionary<IComparable, IComparable>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _keyIx.Read(kvp.keyId);
                    var vInfo = _valIx.Read(kvp.valId);
                    var key = _keyReader.Read(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = _valReader.Read(vInfo.offset, vInfo.len, vInfo.dataType);

                    doc[key] = val;
                }

                doc["__docid"] = d.Key;
                doc["__score"] = d.Value;

                yield return doc;
            }
        }
    }
}
