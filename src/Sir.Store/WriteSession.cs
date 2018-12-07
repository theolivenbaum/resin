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
    public class WriteSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly StreamWriter _log;

        public WriteSession(
            string collectionId, 
            SessionFactory sessionFactory) : base(collectionId, sessionFactory)
        {
            _log = Logging.CreateWriter("writesession");

            var collection = collectionId.ToHash();

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collection)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collection)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collection)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collection)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collection)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collection)));

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
        }

        public async Task<IList<ulong>> Write(WriteJob job)
        {
            var docIds = new List<ulong>();
            var docCount = 0;
            var timer = new Stopwatch();

            timer.Start();

            foreach (var model in job.Documents)
            {
                var docId = await Write(model);

                docIds.Add(docId);

                docCount++;
            }

            _log.Log(string.Format("processed {0} documents in {1}", docCount, timer.Elapsed));

            return docIds;
        }

        public async Task<ulong> Write(IDictionary model)
        {
            var timer = new Stopwatch();
            timer.Start();

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
                    var keyInfo = await _keys.Append(keyStr);
                    keyId = await _keyIx.Append(keyInfo.offset, keyInfo.len, keyInfo.dataType);
                    await SessionFactory.PersistKeyMapping(keyHash, keyId);
                }

                // store value
                var valInfo = await _vals.Append(val);
                valId = await _valIx.Append(valInfo.offset, valInfo.len, valInfo.dataType);

                // store refs to keys and values
                docMap.Add((keyId, valId));
            }

            var docMeta = await _docs.Append(docMap);
            await _docIx.Append(docMeta.offset, docMeta.length);

            model.Add("__docid", docId);

            _log.Log(string.Format("processed document {0} in {1}", docId, timer.Elapsed));

            return docId;
        }
    }

    public class PersistantStorage
    {
        public ulong GetNextDocId()
        {
            throw new NotImplementedException();
        }

        public bool TryGetKeyId(ulong keyHash, out long keyId)
        {
            throw new NotImplementedException();
        }

        public async Task<(long offset, int len, byte dataType)> AppendKey(IComparable value)
        {
            throw new NotImplementedException();
        }

        public async Task<uint> AppendKeyIndexEntry(long offset, int len, byte dataType)
        {
            throw new NotImplementedException();
        }

        public async Task<(long offset, int len, byte dataType)> AppendValue(IComparable value)
        {
            throw new NotImplementedException();
        }

        public async Task<uint> AppendValueIndexEntry(long offset, int len, byte dataType)
        {
            throw new NotImplementedException();
        }

        public async Task<(long offset, int length)> AppendDocument(IList<(long keyId, long valId)> doc)
        {
            throw new NotImplementedException();
        }

        public async Task AppendDocumentIndexEntry(long offset, int len)
        {
            throw new NotImplementedException();
        }
    }
}