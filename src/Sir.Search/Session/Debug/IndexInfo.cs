using System.Collections.Generic;

namespace Sir.Search
{
    public class IndexInfo
    {
        public IEnumerable<GraphInfo> Info { get; }

        public IndexInfo(IEnumerable<GraphInfo> info)
        {
            Info = info;
        }
    }
}