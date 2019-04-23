using System;
using System.Collections;
using System.Collections.Generic;
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
            var numOfDocs = _docIx.NumOfDocs;

            var docIds = Enumerable.Range(1, numOfDocs);

            if (skip > 0)
            {
                docIds = docIds.Skip(skip);
            }

            if (take > 0)
            {
                docIds = docIds.Take(take);
            }

            var dic = docIds.ToDictionary(x => (long)x, y => (float)0);

            return ReadDocs(dic);
        }

        public IEnumerable<IDictionary> ReadDocs(IDictionary<long, float> docs)
        {
            foreach (var d in docs)
            {
                var docInfo = _docIx.Read(d.Key);

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

                doc["___docid"] = d.Key;
                doc["___score"] = d.Value;

                yield return doc;
            }
        }
    }
}
