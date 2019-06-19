using System;
using System.Text;

namespace Sir
{
    public class Vector
    {
        public Memory<int> Values { get; private set; }
        public int Count { get { return Values.Length; } }

        public Vector(Memory<int> values)
        {
            Values = values;
        }

        public virtual string AsString()
        {
            var buf = new char[Values.Length];

            for (int i = 0; i < Values.Length; i++)
            {
                buf[i] = (char)Values.Span[i];
            }

            return new string(buf);
        }
    }

    public class IndexedVector : Vector
    {
        public Memory<int> Index { get; private set; }

        public IndexedVector(Memory<int> index, Memory<int> values):base(values)
        {
            Index = index;
        }

        public override string AsString()
        {
            var w = new StringBuilder();
            var ix = Index.ToArray();
            var vals = Values.ToArray();

            for (int i = 0; i < ix.Length;i++)
            {
                var c = ix[i];
                var val = vals[i];

                for(int ii = 0; ii < val; ii++)
                {
                    w.Append(char.ConvertFromUtf32(c));
                }
            }

            return w.ToString();
        }

        public static IndexedVector[] UnitVectors(int dimensions)
        {
            var bases = new IndexedVector[dimensions];

            for (int i = 0; i < dimensions; i++)
            {
                Span<int> vectorValues = new int[1];
                vectorValues.Fill(1);

                Span<int> vectorIndex = new int[1];
                vectorIndex.Fill(i);

                bases[i] = new IndexedVector(vectorIndex.ToArray(), vectorValues.ToArray());
            }

            return bases;
        }
    }
}
