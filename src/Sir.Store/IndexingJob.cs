using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class IndexingJob
    {
        public IEnumerable<IDictionary> Documents { get; }

        public IndexingJob(IEnumerable<IDictionary> documents)
        {
            Documents = documents;
        }
    }
}
