using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : DocumentSession, ILogger
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocMapWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;

        public WriteSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory) : base(collectionName, collectionId, sessionFactory)
        {
            var valueFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId));
            var keyFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId));
            var docFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId));
            var valueIndexFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId));
            var keyIndexFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId));
            var docIndexFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId));

            var valueStream = sessionFactory.CreateAppendStream(valueFileName);
            var keyStream = sessionFactory.CreateAppendStream(keyFileName);
            var docStream = sessionFactory.CreateAppendStream(docFileName);
            var valueIndexStream = sessionFactory.CreateAppendStream(valueIndexFileName);
            var keyIndexStream = sessionFactory.CreateAppendStream(keyIndexFileName);
            var docIndexStream = sessionFactory.CreateAppendStream(docIndexFileName);

            _vals = new ValueWriter(valueStream);
            _keys = new ValueWriter(keyStream);
            _docs = new DocMapWriter(docStream);
            _valIx = new ValueIndexWriter(valueIndexStream);
            _keyIx = new ValueIndexWriter(keyIndexStream);
            _docIx = new DocIndexWriter(docIndexStream);
        }

        public long Write(IDictionary doc)
        {
            doc["__created"] = DateTime.Now.ToBinary();

            return DoWrite(doc);
        }

        /// <summary>
        /// Fields prefixed with "___" will not be stored.
        /// The "___docid" field, if it exists, will be persisted as "__original", if that field doesn't already exist.
        /// </summary>
        /// <returns>Document ID</returns>
        public long DoWrite(IDictionary model)
        {
            var timer = new Stopwatch();
            timer.Start();

            var docMap = new List<(long keyId, long valId)>();

            if (model.Contains("___docid") && !model.Contains("__original"))
            {
                model.Add("__original", model["___docid"]);
            }

            foreach (var key in model.Keys)
            {
                var val = model[key];

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
                long keyId, valId;

                if (!SessionFactory.TryGetKeyId(CollectionId, keyHash, out keyId))
                {
                    lock (_syncNewKeys)
                    {
                        if (!SessionFactory.TryGetKeyId(CollectionId, keyHash, out keyId))
                        {
                            // We have a new key!

                            // store key
                            var keyInfo = _keys.Append(keyStr);
                            keyId = _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                            SessionFactory.PersistKeyMapping(CollectionId, keyHash, keyId);
                        }
                    }
                }

                // store value
                var valInfo = _vals.Append(val);
                valId = _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                // store refs to keys and values
                docMap.Add((keyId, valId));
            }

            var docMeta = _docs.Append(docMap);
            var docId = _docIx.Append(docMeta.length);

            model["___docid"] = docId;

            this.Log(string.Format("processed document {0} in {1}", docId, timer.Elapsed));

            return docId;
        }

        private static readonly object _syncNewKeys = new object();

        public override void Dispose()
        {
            _vals.Dispose();
            _keys.Dispose();
            _docs.Dispose();
            _valIx.Dispose();
            _keyIx.Dispose();
            _docIx.Dispose();

            base.Dispose();
        }
    }
}