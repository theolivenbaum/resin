using System;
using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class WriteJob
    {
        public string CollectionName { get; }
        public IEnumerable<IDictionary> Documents { get; }
        public Guid Id { get; }

        public WriteJob(string collectionName, IEnumerable<IDictionary> documents)
        {
            CollectionName = collectionName;
            Id = Guid.NewGuid();
            Documents = documents;
        }
    }
}
