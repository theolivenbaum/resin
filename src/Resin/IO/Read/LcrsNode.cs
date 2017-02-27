using System;
using System.Diagnostics;

namespace Resin.IO.Read
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public struct LcrsNode : IEquatable<LcrsNode>
    {
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Value.GetHashCode();
                hashCode = (hashCode * 397) ^ HaveSibling.GetHashCode();
                hashCode = (hashCode * 397) ^ HaveChild.GetHashCode();
                hashCode = (hashCode * 397) ^ EndOfWord.GetHashCode();
                hashCode = (hashCode * 397) ^ Depth;
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

        public readonly char Value;
        public readonly bool HaveSibling;
        public readonly bool HaveChild;
        public readonly bool EndOfWord;
        public readonly int Depth;

        public LcrsNode(string data)
        {
            Value = data[0];
            HaveSibling = data[1] == '1';
            HaveChild = data[2] == '1';
            EndOfWord = data[3] == '1';
            Depth = int.Parse(data.Substring(4));
        }

        public LcrsNode(LcrsTrie trie, int depth)
        {
            Value = trie.Value;
            HaveSibling = trie.RightSibling != null;
            HaveChild = trie.LeftChild != null;
            EndOfWord = trie.EndOfWord;
            Depth = depth;
        }

        public static LcrsNode MinValue
        {
            get { return new LcrsNode("\00000"); }
        }

        public bool Equals(LcrsNode other)
        {
            return Value == other.Value && HaveSibling.Equals(other.HaveSibling) && HaveChild.Equals(other.HaveChild) && EndOfWord.Equals(other.EndOfWord) && Depth == other.Depth;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is LcrsNode && Equals((LcrsNode)obj);
        }
    }
}