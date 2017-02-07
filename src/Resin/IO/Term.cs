using System;

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

        public bool Equals(Term other)
        {
            if (other == null) return false;
            return other.Field == Field && other.Word == Word;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Field.GetHashCode();
            hash = (hash * 7) + Word.GetHashCode();
            return hash;
        }

        public int CompareTo(Term other)
        {
            if (Equals(other)) return 0;
            if (string.Compare(Field, other.Field, StringComparison.Ordinal) == 0)
            {
                return Word.CompareTo(other.Word);
            }
            return -1;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Word);
        }
    }
}