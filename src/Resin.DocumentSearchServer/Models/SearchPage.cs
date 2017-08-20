using System.Collections.Generic;

namespace Resin.SearchServer
{
    public class SearchPage
    {
        public string Query { get; set; }
        public ResultInfo ResultInfo { get; set; }
        public IEnumerable<SearchHit> SearchHits { get; set; }
    }
}
