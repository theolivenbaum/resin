using System;
using System.Text;

namespace Sir
{
    public class Vector
    {
        public ArraySegment<int> Values { get; private set; }
        public int Count { get; }

        public Vector(ArraySegment<int> values)
        {
            Values = values;
            Count = Values.Count;
        }

        public override string ToString()
        {
            var buf = new StringBuilder();

            for (int i = 0; i < Values.Count; i++)
            {
                buf.Append((char)Values[i]);
            }

            return buf.ToString();
        }
    }

    public class IndexedVector : Vector
    {
        public ArraySegment<int> Index { get; }

        public IndexedVector(ArraySegment<int> index, ArraySegment<int> values) : base(values)
        {
            Index = index;
        }

        public override string ToString()
        {
            var buf = new StringBuilder();

            for (int i = 0; i < Index.Count; i++)
            {
                buf.Append((char)Index[i], Values[i]);
            }

            return buf.ToString();
        }
    }
}
