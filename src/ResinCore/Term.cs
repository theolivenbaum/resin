using System;
using System.Diagnostics;

namespace Resin
{
    [DebuggerDisplay("{Field}:{Word}")]
    public class Term : IEquatable<Term>, IComparable<Term>
    {
        public string Field { get; private set; }
        public Word Word { get; private set; }

        public Term(string field, Word word)
        {
            if (field == null) throw new ArgumentNullException("field");

            Field = field;
            Word = word;
        }

        public bool Equals(Term other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Field, other.Field) && Word.Value.Equals(other.Word.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((Term)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Field != null ? Field.GetHashCode() : 0) * 397) ^ Word.GetHashCode();
            }
        }

        public int CompareTo(Term other)
        {
            if (Equals(other)) return 0;
            return -1;
        }

        public static bool operator ==(Term left, Term right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Term left, Term right)
        {
            return !Equals(left, right);
        }
    }
}