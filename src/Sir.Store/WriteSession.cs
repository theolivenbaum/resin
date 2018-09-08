using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly PagedPostingsReader _postingsReader;
        private readonly Dictionary<long, VectorNode> _dirty;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;

        public WriteSession(
            ulong collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("session");

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            //PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            //VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetCollectionIndex(collectionId);

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
            _postingsReader = new PagedPostingsReader(PostingsStream);
            _dirty = new Dictionary<long, VectorNode>();
        }

        public void Write(IEnumerable<IDictionary> models, bool writeToIndex = false)
        {
            foreach (var model in models)
            {
                var docId = _docIx.GetNextDocId();
                var docMap = new List<(long keyId, long valId)>();

                foreach (var key in model.Keys)
                {
                    var keyStr = key.ToString();
                    var keyHash = keyStr.ToHash();
                    var val = (IComparable)model[key];
                    var str = val as string;
                    long keyId, valId;

                    if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
                    {
                        // We have a new key!

                        // store key
                        var keyInfo = _keys.Append(keyStr);
                        keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                        SessionFactory.PersistKeyMapping(keyHash, keyId);
                    }

                    // store value
                    var valInfo = _vals.Append(val);
                    valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                    // store refs to keys and values
                    docMap.Add((keyId, valId));
                }

                var docMeta = _docs.Append(docMap);
                _docIx.Append(docMeta.offset, docMeta.length);

                model.Add("__docid", docId);
            }
        }
    }
}