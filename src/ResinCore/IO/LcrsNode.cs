using StreamIndex;
using System;
using System.Diagnostics;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsNode : IEquatable<LcrsNode>
    {
        public readonly char Value;
        public readonly bool HaveSibling;
        public readonly bool HaveChild;
        public readonly bool EndOfWord;
        public readonly short Depth;
        public readonly int Weight;
        public readonly BlockInfo? PostingsAddress;
        public readonly LcrsTrie Tree;

        public LcrsNode(LcrsTrie trie, short depth, int weight, BlockInfo? postingsAddress)
        {
            Tree = trie;
            Value = trie.Value;
            HaveSibling = trie.RightSibling != null;
            HaveChild = trie.LeftChild != null;
            EndOfWord = trie.EndOfWord;
            Depth = depth;
            Weight = weight;
            PostingsAddress = postingsAddress;
        }

        public LcrsNode(char value, bool haveSibling, bool haveChild, bool endOfWord, short depth, int weight, BlockInfo? postingsAddress)
        {
            Tree = null;
            Value = value;
            HaveSibling = haveSibling;
            HaveChild = haveChild;
            EndOfWord = endOfWord;
            Depth = depth;
            Weight = weight;
            PostingsAddress = postingsAddress;
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
            if (ReferenceEquals(null, left) && !ReferenceEquals(null, right)) return false;
            if (ReferenceEquals(null, left)) return false;
            if (ReferenceEquals(null, right)) return false;
            return left.Equals(right);
        }

        public static bool operator !=(LcrsNode left, LcrsNode right)
        {
            if (ReferenceEquals(right, left)) return false;
            if (ReferenceEquals(null, left)) return true;
            if (ReferenceEquals(null, right)) return true;
            return !left.Equals(right);
        }

        public static LcrsNode MinValue
        {
            get { return new LcrsNode(new LcrsTrie(), 0, 0, null); }
        }

        public bool Equals(LcrsNode other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other)) return true;

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