using System;

namespace Sir
{
    public class Vector
    {
        public Memory<int> Index { get; private set; }
        public Memory<int> Values { get; private set; }
        public int Count { get { return Index.Length; } }

        public Vector(Memory<int> index, Memory<int> values)
        {
            Index = index;
            Values = values;
        }
    }
}
