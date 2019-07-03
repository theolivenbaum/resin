using System.Collections.Generic;

namespace Sir.Store
{
    public class Job
    {
        public string Collection { get; private set; }
        public IEnumerable<IDictionary<string, object>> Documents { get; private set; }

        public Job(string collection, IEnumerable<IDictionary<string, object>> documents)
        {
            Collection = collection;
            Documents = documents;
        }
    }
}