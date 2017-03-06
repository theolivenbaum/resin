using System;

namespace Resin.IO
{
    [Serializable]
    public struct Word : IEquatable<Word>
    {
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

        public readonly string Value;

        [NonSerialized]
        public int Distance;

        public Word(string value)
        {
            Value = value;
            Distance = 0;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}