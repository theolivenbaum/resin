using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : DocumentSession, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocMapWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly TermIndexSession _indexSession;

        public WriteSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            TermIndexSession indexSession,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)), int.Parse(config.Get("value_stream_buffer_size")));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _config = config;
            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocMapWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
            _indexSession = indexSession;
        }

        public override void Dispose()
        {
            _keys.Dispose();
            _vals.Dispose();
            _keyIx.Dispose();
            _valIx.Dispose();
            _docs.Dispose();
            _docIx.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Fields prefixed with "___" will not be stored.
        /// </summary>
        /// <returns>Document ID</returns>
        public long Write(IDictionary<string, object> document)
        {
            document["__created"] = DateTime.Now.ToBinary();

            var docMap = new List<(long keyId, long valId)>();
            var docId = _docIx.GetNextDocId();

            foreach (var key in document.Keys)
            {
                var val = document[key];

                if (val == null)
                {
                    continue;
                }

                var keyStr = key.ToString();

                if (keyStr.StartsWith("___"))
                {
                    continue;
                }

                var keyHash = keyStr.ToHash();
                long keyId;

                if (!SessionFactory.TryGetKeyId(CollectionId, keyHash, out keyId))
                {
                    // We have a new key!

                    // store key
                    var keyInfo = _keys.Append(keyStr);

                    keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                    SessionFactory.PersistKeyMapping(CollectionId, keyHash, keyId);
                }

                // store value
                var valInfo = _vals.Append(val);
                var valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                // store refs to keys and values
                docMap.Add((keyId, valId));

                // index
                //if (!keyStr.StartsWith("_") && valInfo.dataType == DataType.STRING)
                //{
                //    _indexSession.Put(docId, keyId, (string) val);
                //}
            }

            var docMeta = _docs.Append(docMap);

            _docIx.Append(docMeta.offset, docMeta.length);

            return docId;
        }
    }
}