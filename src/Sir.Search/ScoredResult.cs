using System.Collections.Generic;

namespace Sir.Search
{
    public class ScoredResult
    {
        public IList<KeyValuePair<(ulong, long), double>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
