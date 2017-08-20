using System.Collections.Generic;
using Resin.Documents;

namespace Resin
{
    public class InMemoryDocumentStream : DocumentStream
    {
        private readonly IEnumerable<DocumentTableRow> _documents;

        public InMemoryDocumentStream(IEnumerable<DocumentTableRow> documents, string primaryKeyFieldName = null) 
            : base(primaryKeyFieldName)
        {
            _documents = documents;
        }
        public override IEnumerable<DocumentTableRow> ReadSource()
        {
            return ReadSourceAndAssignPk(_documents);
        }
    }
}
