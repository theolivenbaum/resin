using Sir.KeyValue;
using System;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Write session targeting a single collection.
    /// </summary>
    public class WriteSession : CollectionSession, ILogger, IDisposable
    {
        private readonly IndexSession _termIndexSession;
        private readonly DocumentWriter _streamWriter;
        private readonly IStringModel _model;

        public WriteSession(
            ulong collectionId,
            SessionFactory sessionFactory,
            DocumentWriter streamWriter,
            IConfigurationProvider config,
            IStringModel model,
            IndexSession termIndexSession) : base(collectionId, sessionFactory)
        {
            _termIndexSession = termIndexSession;
            _streamWriter = streamWriter;
            _model = model;
        }

        public void Dispose()
        {
            _termIndexSession.Dispose();
            _streamWriter.Dispose();
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
                        _termIndexSession.Put(docId, kvmap.keyId, (string)val);
                    }
                }

                var docMeta = _streamWriter.PutDocumentMap(docMap);

                _streamWriter.PutDocumentAddress(docMeta.offset, docMeta.length);

                document["___docid"] = docId;
            }

            return _termIndexSession.GetIndexInfo();
        }
    }
}