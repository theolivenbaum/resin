using System.Collections.Generic;
using Resin.Analysis;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class DocumentUpsertOperation : UpsertOperation
    {
        private readonly IEnumerable<IDictionary<string, string>> _documents;

        public DocumentUpsertOperation(string directory, IAnalyzer analyzer, bool compression, string primaryKey, IEnumerable<IDictionary<string, string>> documents) 
            : base(directory, analyzer, compression, primaryKey)
        {
            _documents = documents;
        }

        protected override IEnumerable<Document> ReadSource()
        {
            return _documents.Select(d=>new Document(d));
        }
    }
}