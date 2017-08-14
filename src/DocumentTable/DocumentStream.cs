using log4net;
using System;
using System.Collections.Generic;

namespace DocumentTable
{
    public abstract class DocumentStream
    {
        public abstract IEnumerable<Document> ReadSource();

        protected static readonly ILog Log = LogManager.GetLogger(typeof(DocumentStream));

        private readonly Dictionary<ulong, object> _primaryKeys;
        private string _primaryKeyFieldName;

        public string PrimaryKeyFieldName { get { return _primaryKeyFieldName; } }

        protected DocumentStream(string primaryKeyFieldName = null)
        {
            _primaryKeys = new Dictionary<UInt64, object>();
            _primaryKeyFieldName = primaryKeyFieldName;
        }

        protected IEnumerable<Document> ReadSourceAndAssignPk(
            IEnumerable<Document> documents)
        {
            var autoGeneratePk = string.IsNullOrWhiteSpace(_primaryKeyFieldName);

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
                    Log.WarnFormat("Found multiple occurrences of documents with pk value of {0}. First occurrence will be stored.",
                        pkVal);
                }
                else
                {
                    _primaryKeys.Add(hash, null);

                    document.Hash = hash;

                    yield return document;
                }
            }
        }
    }
}