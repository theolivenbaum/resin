using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class Job
    {
        public string Collection { get; private set; }
        public IEnumerable<IDictionary> Documents { get; private set; }
        public bool Done { get; set; } 

        public Job(string collection, IEnumerable<IDictionary> documents)
        {
            Collection = collection;
            Documents = documents;
        }
    }
}