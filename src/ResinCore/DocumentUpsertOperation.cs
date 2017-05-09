using System.Collections.Generic;
using Resin.Analysis;
using System.Linq;
using Resin.IO;
using System;
using Resin.Sys;

namespace Resin
{
    public class DocumentUpsertOperation : UpsertOperation
    {
        private readonly IEnumerable<Document> _documents;
        private readonly string _primaryKey;
        private readonly bool _autoGeneratePk;

        public DocumentUpsertOperation(
            string directory, IAnalyzer analyzer, Compression compression, string primaryKey, IEnumerable<Document> documents) 
            : base(directory, analyzer, compression)
        {
            _documents = documents;
            _primaryKey = primaryKey;
            _autoGeneratePk = string.IsNullOrWhiteSpace(primaryKey);
        }

        protected override IEnumerable<Document> ReadSource()
        {
            foreach (var document in _documents)
            {
                string pkVal;

                if (_autoGeneratePk)
                {
                    pkVal = Guid.NewGuid().ToString();
                }
                else
                {
                    pkVal = document.Fields.First(f => f.Key == _primaryKey).Value;
                }

                var hash = pkVal.ToHash();

                if (Pks.ContainsKey(hash))
                {
                    Log.WarnFormat("Found multiple occurrences of documents with pk value of {0} (id:{1}). Only first occurrence will be stored.",
                        pkVal, document.Id);
                }
                else
                {
                    Pks.Add(hash, null);

                    document.Hash = hash;

                    yield return document;
                }
            }
        }
    }
}