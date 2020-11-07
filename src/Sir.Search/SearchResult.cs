using Sir.VectorSpace;
using System.Collections.Generic;

namespace Sir.Search
{
    public class SearchResult
    {
        public IQuery Query { get; set; }
        public Term QueryTerm { get; set; }
        public long Total { get; set; }
        public IEnumerable<IDictionary<string, object>> Documents { get; set; }
    }
}
