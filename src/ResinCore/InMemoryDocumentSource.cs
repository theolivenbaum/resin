using System.Collections.Generic;

namespace Resin
{
    public class InMemoryDocumentSource : DocumentSource
    {
        private readonly IEnumerable<Document> _documents;

        public InMemoryDocumentSource(IEnumerable<Document> documents) 
        {
            _documents = documents;
        }
        public override IEnumerable<Document> ReadSource()
        {
            return _documents;
        }
    }
}
