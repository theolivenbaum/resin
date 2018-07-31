using System;

namespace Sir
{
    [System.Diagnostics.DebuggerDisplay("{Key}:{Value}")]
    public class Term
    {
        public IComparable Key { get; private set; }
        public IComparable Value { get; set; }
        public uint KeyId { get; set; }

        public Term(IComparable key, IComparable value)
        {
            Key = key;
            Value = value;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, Value);
        }
    }
}