using System.Collections.Generic;

namespace Sir.Store
{
    public class ScoredResult
    {
        public IList<KeyValuePair<long, double>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
