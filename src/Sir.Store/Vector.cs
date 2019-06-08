using System.Collections.Generic;

namespace Sir
{
    public class Vector
    {
        public IList<long> Index { get; private set; }
        public IList<int> Values { get; private set; }
        public int Count { get { return Index.Count; } }

        public Vector(IList<long> index, IList<int> values)
        {
            Index = index;
            Values = values;
        }
    }
}
