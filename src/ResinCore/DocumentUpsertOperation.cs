using System.Collections.Generic;
using Resin.Analysis;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class DocumentUpsertOperation : UpsertOperation
    {
        private readonly IEnumerable<IList<Field>> _documents;

        public DocumentUpsertOperation(string directory, IAnalyzer analyzer, Compression compression, string primaryKey, IEnumerable<IList<Field>> documents) 
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