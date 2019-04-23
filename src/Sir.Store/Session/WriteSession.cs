using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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
            ValueStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateAsyncAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocMapWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
        }

        public async Task<long> Write(IDictionary doc)
        {
            doc["__created"] = DateTime.Now.ToBinary();

            return await DoWrite(doc);
        }

        public async Task<IEnumerable<long>> Write(IEnumerable<IDictionary> docs)
        {
            var docIds = new List<long>();
            var docCount = 0;
            var timer = new Stopwatch();

            timer.Start();

            foreach (var model in docs)
            {
                model["__created"] = DateTime.Now.ToBinary();

                var docId = await Write(model);

                docIds.Add(docId);

                docCount++;
            }

            this.Log(string.Format("processed {0} documents in {1}", docCount, timer.Elapsed));

            return docIds;
        }

        private static readonly object _syncNewKeys = new object();

        /// <summary>
        /// Fields prefixed with "___" will not be stored.
        /// The "___docid" field, if it exists, will be persisted as "__original", if that field doesn't already exist.
        /// </summary>
        /// <returns>Document ID</returns>
        public async Task<long> DoWrite(IDictionary model)
        {
            var timer = new Stopwatch();
            timer.Start();

            var docId = _docIx.GetNextDocId();
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
                var valInfo = await _vals.AppendAsync(val);
                valId = await _valIx.AppendAsync(valInfo.offset, valInfo.len, valInfo.dataType);

                // store refs to keys and values
                docMap.Add((keyId, valId));
            }

            var docMeta = await _docs.Append(docMap);
            await _docIx.Append(docMeta.offset, docMeta.length);

            model["___docid"] = docId;

            this.Log(string.Format("processed document {0} in {1}", docId, timer.Elapsed));

            return docId;
        }
    }
}