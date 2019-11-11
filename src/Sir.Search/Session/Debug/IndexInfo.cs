using System.Collections.Generic;

namespace Sir.Search
{
    public class IndexInfo
    {
        public IEnumerable<GraphInfo> Info { get; }
        public int QueueLength { get; }

        public IndexInfo(IEnumerable<GraphInfo> info, int queueLength)
        {
            Info = info;
            QueueLength = queueLength;
        }
    }
}