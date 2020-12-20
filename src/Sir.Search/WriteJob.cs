using Sir.VectorSpace;
using System.Collections.Generic;

namespace Sir.Search
{
    public class WriteJob<T>
    {
        public IModel<T> Model { get; }
        public ulong CollectionId { get; private set; }
        public IEnumerable<Document> Documents { get; private set; }

        public WriteJob(
            ulong collectionId, 
            IEnumerable<Document> documents,
            IModel<T> model)
        {
            Model = model;
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}