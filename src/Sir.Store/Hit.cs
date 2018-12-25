using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    public class Hit
    {
        public SortedList<int, byte> Embedding { get; set; }
        public float Score { get; set; }
        public long PostingsOffset { get; set; }

        public override string ToString()
        {
            return string.Join(string.Empty, Embedding.Keys.Select(x => char.ConvertFromUtf32(x)).ToArray());
        }
    }
}
