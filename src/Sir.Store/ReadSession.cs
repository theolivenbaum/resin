using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    public class ReadSession : Session
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly PostingsReader _postingsReader;

        public ReadSession(string directory, ulong collectionId, SessionFactory sessionFactory) 
            : base(directory, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.ValueStream;
            KeyStream = sessionFactory.CreateReadStream(string.Format("{0}.key", collectionId));
            DocStream = sessionFactory.CreateReadStream(string.Format("{0}.docs", collectionId));
            ValueIndexStream = sessionFactory.ValueIndexStream;
            KeyIndexStream = sessionFactory.CreateReadStream(string.Format("{0}.kix", collectionId));
            DocIndexStream = sessionFactory.CreateReadStream(string.Format("{0}.dix", collectionId));
            PostingsStream = sessionFactory.CreateReadStream(string.Format("{0}.pos", collectionId));
            VectorStream = sessionFactory.CreateReadStream(string.Format("{0}.vec", collectionId));
            Index = sessionFactory.GetIndex(collectionId);

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new PostingsReader(PostingsStream);
        }

        public IEnumerable<IDictionary> Read(Query query)
        {
            IEnumerable<ulong> result = null;

            while (query != null)
            {
                var keyHash = query.Term.Key.ToString().ToHash();
                var ix = GetIndex(keyHash);
                var match = ix.ClosestMatch(query.Term.Value.ToString());
                var docIds = _postingsReader.Read(match.PostingsOffset, match.PostingsSize);

                if (result == null)
                {
                    result = docIds;
                }
                else
                {
                    if (query.And)
                    {
                        result = (from doc in result
                                  join id in docIds on doc equals id
                                  select doc);
                        
                    }
                    else if (query.Not)
                    {
                        result = result.Except(docIds);
                    }
                    else // Or
                    {
                        result = result.Concat(docIds).Distinct();
                    }
                }
                query = query.Next;
            }

            return ReadDocs(result);
        }

        private IEnumerable<IDictionary> ReadDocs(IEnumerable<ulong> docIds)
        {
            foreach (var docId in docIds)
            {
                var docInfo = _docIx.Read(docId);
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

                yield return doc;
            }
        }
    }
}
