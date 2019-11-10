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
    public class WriteSession : ILogger, IDisposable
    {
        private readonly SessionFactory _sessionFactory;
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
            _sessionFactory = sessionFactory;
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

        /// <summary>
        /// Fields prefixed with "_" will not be indexed.
        /// Fields prefixed with "__" will not be stored.
        /// </summary>
        /// <returns>Document ID</returns>
        public IndexInfo Write(IEnumerable<IDictionary<string, object>> documents)
        {
            foreach (var document in documents)
            {
                document["_created"] = DateTime.Now.ToBinary();

                var docMap = new List<(long keyId, long valId)>();
                var docId = _streamWriter.PeekNextDocId();
                var indexFields = new List<(long docId, long keyId, string val)>();

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

                _streamWriter.PutDocumentAddress(docMeta.offset, docMeta.length);

                document["___docid"] = docId;
            }

            return _indexSession.GetIndexInfo();
        }
    }
}