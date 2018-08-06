using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Create a read session targetting a single collection ("table").
    /// </summary>
    public class ReadSession : CollectionSession
    {
        private readonly DocIndexReader _docIx;
        private readonly DocReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly PostingsReader _postingsReader;

        public ReadSession(string directory, ulong collectionId, LocalStorageSessionFactory sessionFactory) 
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
            IDictionary<ulong, double> result = null;

            while (query != null)
            {
                var keyHash = query.Term.Key.ToString().ToHash();
                var ix = GetIndex(keyHash);
                var match = ix.ClosestMatch(query.Term.Value.ToString());

                if (match.Highscore >= 0.8d)
                {
                    var docIds = _postingsReader.Read(match.PostingsOffset, match.PostingsSize)
                        .ToDictionary(x => x, y => match.Highscore);

                    if (result == null)
                    {
                        result = docIds;
                    }
                    else
                    {
                        if (query.And)
                        {
                            result = (from doc in result
                                      join x in docIds on doc.Key equals x.Key
                                      select doc).ToDictionary(x=>x.Key, y=>y.Value);

                        }
                        else if (query.Not)
                        {
                            foreach (var id in docIds)
                            {
                                result.Remove(id);
                            }
                        }
                        else // Or
                        {
                            foreach (var id in docIds)
                            {
                                result[id.Key] = id.Value; 
                            }
                        }
                    }
                }
                query = query.Next;
            }

            if (result == null)
            {
                return Enumerable.Empty<IDictionary>();
            }

            return ReadDocs(result);
        }

        private IEnumerable<IDictionary> ReadDocs(IDictionary<ulong, double> docs)
        {
            foreach (var d in docs)
            {
                var docInfo = _docIx.Read(d.Key);
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

                doc["_score"] = d.Value;

                yield return doc;
            }
        }
    }
}
