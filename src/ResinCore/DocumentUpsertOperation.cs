using System.Collections.Generic;
using Resin.Analysis;
using Resin.IO;

namespace Resin
{
    public class DocumentUpsertOperation : UpsertOperation
    {
        private readonly IEnumerable<Document> _documents;

        public DocumentUpsertOperation(
            string directory, IAnalyzer analyzer, Compression compression, string primaryKey, IEnumerable<Document> documents) 
            : base(directory, analyzer, compression, primaryKey)
        {
            _documents = documents;
        }

        protected override IEnumerable<Document> ReadSource()
        {
            return _documents;
        }
    }
}