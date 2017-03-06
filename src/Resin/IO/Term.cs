using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
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

        public int CompareTo(Term other)
        {
            //if (other.Field == Field && other.Word == Word) return 0;
            //if (other.Field == Field && other.Word != Word) return 1;
            //return -1;
            return String.Compare(other.ToString(), ToString(), StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Word);
        }

        public bool Equals(Term other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Field, other.Field) && Word.Equals(other.Word);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Term) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Field != null ? Field.GetHashCode() : 0)*397) ^ Word.GetHashCode();
            }
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

    public class TermComparer : IComparer<Term>
    {
        public int Compare(Term x, Term y)
        {
            return x.CompareTo(y);
        }
    }
}