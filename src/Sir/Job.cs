using System.Collections.Generic;

namespace Sir
{
    public class Job
    {
        public IStringModel Model { get; }
        public ulong CollectionId { get; private set; }
        public IEnumerable<IDictionary<string, object>> Documents { get; private set; }

        public Job(ulong collectionId, IEnumerable<IDictionary<string, object>> documents, IStringModel model)
        {
            Model = model;
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}