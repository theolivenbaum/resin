using System;

namespace Resin.IO
{
    [Serializable]
    public class Term : IEquatable<Term>, IComparable<Term>
    {
        public string Field { get; private set; }
        public string Token { get; private set; }

        public Term(string field, string token)
        {
            Field = field;
            Token = token;
        }
        public bool Equals(Term other)
        {
            if (other == null) return false;
            return other.Field == Field && other.Token == Token;
        }
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Field.GetHashCode();
            hash = (hash * 7) + Token.GetHashCode();
            return hash;
        }

        public int CompareTo(Term other)
        {
            return string.Compare(Token, other.Token, StringComparison.InvariantCulture);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Token);
        }
    }
}