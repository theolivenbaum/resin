using log4net;
using Resin.Sys;
using System;
using System.Collections.Generic;

namespace Resin
{
    public abstract class DocumentStream
    {
        public abstract IEnumerable<Document> ReadSource();

        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentStream));
        private readonly Dictionary<ulong, object> _primaryKeys;
        private string _primaryKeyFieldName;

        public string PrimaryKeyFieldName { get { return _primaryKeyFieldName; } }

        protected DocumentStream(string primaryKeyFieldName = null)
        {
            _primaryKeys = new Dictionary<UInt64, object>();
            _primaryKeyFieldName = primaryKeyFieldName;
        }

        protected IEnumerable<Document> ReadSourceAndAssignIdentifiers(
            IEnumerable<Document> documents)
        {
            var count = 0;
            var autoGeneratePk = _primaryKeyFieldName == null;

            foreach (var document in documents)
            {
                string pkVal;

                if (autoGeneratePk)
                {
                    pkVal = Guid.NewGuid().ToString();
                }
                else
                {
                    pkVal = document.Fields[_primaryKeyFieldName].Value;
                }

                var hash = pkVal.ToHash();

                if (_primaryKeys.ContainsKey(hash))
                {
                    Log.WarnFormat("Found multiple occurrences of documents with pk value of {0} (id:{1}). First occurrence will be stored.",
                        pkVal, document.Id);
                }
                else
                {
                    _primaryKeys.Add(hash, null);

                    document.Hash = hash;
                    document.Id = count++;

                    yield return document;
                }
            }
        }
    }
}