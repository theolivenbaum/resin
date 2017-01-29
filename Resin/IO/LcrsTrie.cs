using System;
using System.Diagnostics;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsTrie
    {
        public LcrsTrie RightSibling { get; set; }
        public LcrsTrie LeftChild { get; set; }
        public char Value { get; private set; }
        public bool EndOfWord { get; private set; }

        public LcrsTrie(char value, bool endOfWord)
        {
            Value = value;
            EndOfWord = endOfWord;
        }

        public void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("word");

            var key = path[0];
            var eow = path.Length == 1;

            LcrsTrie node;
            if (!TryGetChild(key, out node))
            {
                node = new LcrsTrie(key, eow);
                var sibling = LeftChild;
                LeftChild = node;
                LeftChild.RightSibling = sibling;
            }
            else
            {
                if (!node.EndOfWord)
                {
                    node.EndOfWord = eow;
                }
            }

            if (!eow)
            {
                node.Add(path.Substring(1));
            }
        }

        private bool TryGetChild(char c, out LcrsTrie node)
        {
            if (LeftChild == null)
            {
                node = null;
                return false;
            }

            if (LeftChild.Value.Equals(c))
            {
                node = LeftChild;
                return true;
            }

            if (RightSibling == null)
            {
                node = null;
                return false;
            }

            return RightSibling.TryGetSibling(c, out node);
        }

        private bool TryGetSibling(char c, out LcrsTrie node)
        {
            if (RightSibling == null)
            {
                node = null;
                return false;
            }

            if (RightSibling.Value.Equals(c))
            {
                node = RightSibling;
                return true;
            }

            if (RightSibling == null)
            {
                node = null;
                return false;
            }

            return RightSibling.TryGetSibling(c, out node);
        }
    }
}