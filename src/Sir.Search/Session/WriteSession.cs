using Sir.Document;
using Sir.KeyValue;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Search
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : IDisposable
    {
        private readonly IndexSession _indexSession;
        private readonly DocumentWriter _streamWriter;
        private readonly IStringModel _model;
        private readonly FileStream _lockFile;

        public WriteSession(
            ulong collectionId,
            SessionFactory sessionFactory,
            DocumentWriter streamWriter,
            IStringModel model,
            IndexSession termIndexSession)
        {
            _indexSession = termIndexSession;
            _streamWriter = streamWriter;
            _model = model;
            _lockFile = sessionFactory.CreateLockFile(collectionId);
        }

        public void Dispose()
        {
            _indexSession.Dispose();
            _streamWriter.Dispose();
            _lockFile.Dispose();
        }

        public IndexInfo GetIndexInfo()
        {
            return _indexSession.GetIndexInfo();
        }

        /// <summary>
        /// Fields prefixed with "_" will not be indexed.
        /// Fields prefixed with "__" will not be stored.
        /// </summary>
        /// <returns>Document ID</returns>
        public void Write(IDictionary<string, object> document)
        {
            document["_created"] = DateTime.Now.ToBinary();

            var docMap = new List<(long keyId, long valId)>();
            var docId = _streamWriter.GetNextDocId();

            foreach (var key in document.Keys)
            {
                if (key.StartsWith("__"))
                {
                    continue;
                }

                var val = document[key];

                if (val == null)
                {
                    continue;
                }

                byte dataType;

                // store k/v
                var kvmap = _streamWriter.Put(key, val, out dataType);

                // store refs to k/v pair
                docMap.Add(kvmap);

                // add to index
                if (dataType == DataType.STRING && key.StartsWith("_") == false)
                {
                    _indexSession.Put(docId, kvmap.keyId, (string)val);
                }
            }

            var docMeta = _streamWriter.PutDocumentMap(docMap);

            _streamWriter.PutDocumentAddress(docId, docMeta.offset, docMeta.length);

            document["___docid"] = docId;
        }
    }
}