using System.Collections.Generic;

namespace Sir.Search
{
    public class WriteJob
    {
        public ITextModel Model { get; }
        public ulong CollectionId { get; private set; }
        public IEnumerable<Document> Documents { get; private set; }

        public WriteJob(
            ulong collectionId, 
            IEnumerable<Document> documents, 
            ITextModel model)
        {
            Model = model;
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}