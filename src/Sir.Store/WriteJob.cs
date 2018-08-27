using System;
using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class WriteJob
    {
        public ulong CollectionId { get; }
        public IEnumerable<IDictionary> Documents { get; }
        public Guid Id { get; }

        public WriteJob(ulong collectionId, IEnumerable<IDictionary> documents)
        {
            CollectionId = collectionId;
            Id = Guid.NewGuid();
            Documents = documents;
        }
    }

    public class IndexJob
    {
        public ulong CollectionId { get; }
        public IEnumerable<IDictionary> Documents { get; }

        public IndexJob(ulong collectionId, IEnumerable<IDictionary> documents)
        {
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}
