using System.Collections.Generic;
using Resin.Documents;

namespace Resin
{
    public class InMemoryDocumentStream : DocumentStream
    {
        private readonly IEnumerable<Document> _documents;

        public InMemoryDocumentStream(IEnumerable<Document> documents, string primaryKeyFieldName = null) 
            : base(primaryKeyFieldName)
        {
            _documents = documents;
        }
        public override IEnumerable<Document> ReadSource()
        {
            return ReadSourceAndAssignPk(_documents);
        }
    }
}
