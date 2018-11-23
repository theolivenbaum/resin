using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class IndexingJob
    {
        public string CollectionId { get; }
        public IEnumerable<IDictionary> Documents { get; }

        public IndexingJob(string collectionId, IEnumerable<IDictionary> documents)
        {
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}
