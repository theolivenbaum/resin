using System.Collections.Generic;

namespace Sir.Store
{
    public class Job
    {
        public IStringModel Model { get; }
        public string Collection { get; private set; }
        public IEnumerable<IDictionary<string, object>> Documents { get; private set; }

        public Job(string collection, IEnumerable<IDictionary<string, object>> documents, IStringModel model)
        {
            Model = model;
            Collection = collection;
            Documents = documents;
        }
    }
}