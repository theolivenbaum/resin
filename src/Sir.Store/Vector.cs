using System;
using System.Runtime.InteropServices;

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

        public string AsString()
        {
            var buf = new char[Values.Length];

            for (int i = 0; i < Values.Length; i++)
            {
                buf[i] = (char)Values.Span[i];
            }

            return new string(buf);
        }

        public override string ToString()
        {
            return AsString();
        }
    }
}
