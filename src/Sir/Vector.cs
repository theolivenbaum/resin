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

        public override string ToString()
        {
            var buf = new char[Index.Length];

            for (int i = 0; i < Index.Length; i++)
            {
                buf[i] = (char)Index[i];
            }

            return new string(buf);
        }
    }
}
