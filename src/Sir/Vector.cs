using System.Text;

namespace Sir
{
    public class Vector
    {
        public int[] Values { get; private set; }
        public int Count { get; }

        public Vector(int[] values)
        {
            Values = values;
            Count = Values.Length;
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
        public int[] Index { get; }

        public IndexedVector(int[] index, int[] values) : base(values)
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
