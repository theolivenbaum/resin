using System;

namespace Resin.IO
{
    [Serializable]
    public struct Word : IEquatable<Word>, IComparable<Word>
    {
        public readonly string Value;
        public int Distance;

        public Word(string value)
        {
            Value = value;
            Distance = 0;
        }

        public static implicit operator string(Word w)
        {
            return w.Value;
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
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator ==(Word left, Word right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Word left, Word right)
        {
            return !left.Equals(right);
        }

        public int CompareTo(Word other)
        {
            return String.Compare(other.Value, Value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}