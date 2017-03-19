using System;

namespace Resin.IO
{
    [Serializable]
    public struct Word : IEquatable<Word>
    {
        public readonly string Value;

        public Word(string value)
        {
            Value = value;
        }

        public bool Equals(Word other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Word && Equals((Word) obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Word left, Word right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Word left, Word right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}