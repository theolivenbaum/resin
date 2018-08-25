using System;
using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class WriteJob
    {
        public ulong CollectionId { get; }
        public IEnumerable<IDictionary> Data { get; private set; }
        public IEnumerable<IDictionary> Remove { get; private set; }
        public Guid Id { get; private set; }
        public WriteJob(ulong collectionId, IEnumerable<IDictionary> data, bool delete = false)
        {
            CollectionId = collectionId;

            if (delete)
            {
                Remove = data;
            }
            else
            {
                Data = data;
            }
            Id = Guid.NewGuid();
        }

        public WriteJob(ulong collectionId, IEnumerable<IDictionary> data, IEnumerable<IDictionary> remove)
        {
            CollectionId = collectionId;
            Data = data;
            Remove = remove;
            Id = Guid.NewGuid();
        }
    }
}
