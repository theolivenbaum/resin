using System;

namespace Resin.IO
{
    [Serializable]
    public class Term : IEquatable<Term>, IComparable<Term>
    {
        public string Field { get; private set; }
        public string Value { get; private set; }
        public Word Word { get; private set; }

        public Term(string field, Word word)
        {
            Field = field;
            Value = word;
            Word = word;
        }

        public bool Equals(Term other)
        {
            if (other == null) return false;
            return other.Field == Field && other.Value == Value;
        }
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Field.GetHashCode();
            hash = (hash * 7) + Value.GetHashCode();
            return hash;
        }

        public int CompareTo(Term other)
        {
            return string.Compare(Value, other.Value, StringComparison.InvariantCulture);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Value);
        }
    }
}