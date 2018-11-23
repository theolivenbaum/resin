using System;

namespace Sir
{
    /// <summary>
    /// A query term.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Key}:{Value}")]
    public class Term
    {
        public IComparable Key { get; private set; }
        public IComparable Value { get; set; }
        public long KeyId { get; set; }

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