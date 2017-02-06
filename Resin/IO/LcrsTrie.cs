using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
                node.RightSibling = LeftChild;
                LeftChild = node;
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
            node = LeftChild;
            
            while (node != null)
            {
                if (node.Value == c)
                {
                    return true;
                }
                node = node.RightSibling;
            }

            node = null;
            return false;
        }

        public IEnumerable<LcrsTrie> GetFoldedChildlist(int segmentLength)
        {
            return GetLeftChildAndAllOfItsSiblings().Fold(segmentLength);
        }

        public IEnumerable<LcrsTrie> GetLeftChildAndAllOfItsSiblings()
        {
            if (LeftChild != null)
            {
                yield return LeftChild;

                var sibling = LeftChild.RightSibling;

                while (sibling != null)
                {
                    yield return sibling;

                    sibling = sibling.RightSibling;
                }
            }
        }

        public IEnumerable<LcrsTrie> GetAllSiblings()
        {
            if (RightSibling != null)
            {
                yield return RightSibling;

                var sibling = RightSibling.RightSibling;

                while (sibling != null)
                {
                    yield return sibling;

                    sibling = sibling.RightSibling;
                }
            }
        }
    }

    public static class LcrsTrieHelper
    {
        public static IEnumerable<LcrsTrie> Fold(this IEnumerable<LcrsTrie> nodes, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException("size");

            var count = 0;

            foreach (var child in nodes.ToList())
            {
                if (count == 0)
                {
                    yield return child;
                }
                else if (count == size)
                {
                    child.RightSibling = null;

                    count = -1;
                }
                count++;
            }
        }
    }
}