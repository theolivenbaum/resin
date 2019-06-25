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

        public virtual string AsString()
        {
            var buf = new char[Values.Length];

            for (int i = 0; i < Values.Length; i++)
            {
                buf[i] = (char)Values[i];
            }

            return new string(buf);
        }
    }

    public class IndexedVector : Vector
    {
        public int[] Index { get; }

        public IndexedVector(int[] index, int[] values) : base(values)
        {
            Index = index;
        }

        public override string AsString()
        {
            var w = new StringBuilder();
            var ix = Index;
            var vals = Values;

            for (int i = 0; i < ix.Length; i++)
            {
                var c = ix[i];
                var val = vals[i];

                for (int ii = 0; ii < val; ii++)
                {
                    w.Append(char.ConvertFromUtf32(c));
                }
            }

            return w.ToString();
        }
    }
}
