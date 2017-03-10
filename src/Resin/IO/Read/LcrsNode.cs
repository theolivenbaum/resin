using System;
using System.Diagnostics;

namespace Resin.IO.Read
{
    [Serializable, DebuggerDisplay("{Value} {EndOfWord}")]
    public struct LcrsNode : IEquatable<LcrsNode>
    {
        public readonly char Value;
        public readonly bool HaveSibling;
        public readonly bool HaveChild;
        public readonly bool EndOfWord;
        public readonly int Depth;
        public readonly int Weight;

        public LcrsNode(string data)
        {
            Value = data[0];
            HaveSibling = data[1] == '1';
            HaveChild = data[2] == '1';
            EndOfWord = data[3] == '1';

            var depthData = data.Substring(4, 10).TrimEnd('0');

            Depth = string.IsNullOrWhiteSpace(depthData) ? 0 : int.Parse(depthData);

            var weightData = data.Substring(14, 10).TrimEnd('0');

            Weight = string.IsNullOrWhiteSpace(weightData) ? 0 : int.Parse(weightData);
        }

        public LcrsNode(LcrsTrie trie, int depth, int weight)
        {
            Value = trie.Value;
            HaveSibling = trie.RightSibling != null;
            HaveChild = trie.LeftChild != null;
            EndOfWord = trie.EndOfWord;
            Depth = depth;
            Weight = weight;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Value.GetHashCode();
                hashCode = (hashCode * 397) ^ HaveSibling.GetHashCode();
                hashCode = (hashCode * 397) ^ HaveChild.GetHashCode();
                hashCode = (hashCode * 397) ^ EndOfWord.GetHashCode();
                hashCode = (hashCode * 397) ^ Depth;
                hashCode = (hashCode * 397) ^ Weight;
                return hashCode;
            }
        }

        public static bool operator ==(LcrsNode left, LcrsNode right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LcrsNode left, LcrsNode right)
        {
            return !left.Equals(right);
        }

        public static LcrsNode MinValue
        {
            get { return new LcrsNode("\000000000000000000000000"); }
        }

        public bool Equals(LcrsNode other)
        {
            return Value == other.Value
                && HaveSibling.Equals(other.HaveSibling) 
                && HaveChild.Equals(other.HaveChild) 
                && EndOfWord.Equals(other.EndOfWord) 
                && Depth == other.Depth
                && Weight == other.Weight;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is LcrsNode && Equals((LcrsNode)obj);
        }
    }
}