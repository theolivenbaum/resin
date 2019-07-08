using System.Collections.Generic;
using System.Text;

namespace Sir
{
    public class Vector
    {
        public IList<int> Values { get; private set; }
        public int Count { get; }

        public Vector(IList<int> values)
        {
            Values = values;
            Count = Values.Count;
        }

        public override string ToString()
        {
            var buf = new StringBuilder();

            for (int i = 0; i < Count; i++)
            {
                buf.Append((char)Values[i]);
            }

            return buf.ToString();
        }
    }

    public class IndexedVector : Vector
    {
        public IList<int> Index { get; }

        public IndexedVector(IList<int> index, IList<int> values) : base(values)
        {
            Index = index;
        }

        public override string ToString()
        {
            var buf = new StringBuilder();

            for (int i = 0; i < Count; i++)
            {
                buf.Append((char)Index[i], Values[i]);
            }

            return buf.ToString();
        }
    }
}
