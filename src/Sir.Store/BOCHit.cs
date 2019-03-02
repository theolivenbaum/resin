using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    public class BOCHit
    {
        public SortedList<long, byte> Embedding { get; set; }
        public float Score { get; set; }
        public IList<long> PostingsOffsets { get; set; }
        public long NodeId { get; set; }
        public IEnumerable<long> Ids { get; set; }

        public override string ToString()
        {
            return string.Join(string.Empty, Embedding.Keys.Select(x => char.ConvertFromUtf32((int)x)).ToArray());
        }
    }
}
