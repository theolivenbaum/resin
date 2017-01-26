using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class BinaryTree
    {
        public BinaryTree RightSibling { get; set; }
        public BinaryTree LeftChild { get; set; }
        public char Value { get; private set; }
        public bool EndOfWord { get; private set; }

        public BinaryTree(char value, bool endOfWord)
        {
            Value = value;
            EndOfWord = endOfWord;
        }

        public void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("word");

            var key = path[0];
            var eow = path.Length == 1;

            BinaryTree node;
            if (!TryGetChild(key, out node))
            {
                node = new BinaryTree(key, eow);
                var sibling = LeftChild;
                LeftChild = node;
                LeftChild.RightSibling = sibling;
            }

            if (!eow)
            {
                node.Add(path.Substring(1));
            }
        }

        private bool TryGetChild(char c, out BinaryTree node)
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

        private bool TryGetSibling(char c, out BinaryTree node)
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

    public static class TreeScanner
    {
        public static bool HasWord(this BinaryTree root, string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("path");

            BinaryTree node;
            if (root.TryGoPath(word, out node))
            {
                return node.EndOfWord;
            }
            return false;
        }

        public static IList<string> StartsWith(this BinaryTree node, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var compressed = new List<string>();
            
            BinaryTree child;
            if (node.TryGoPath(prefix, out child))
            {
                child.LeftChild.Compress(prefix, new List<char>(), compressed);
            }
            
            return compressed;
        }

        public static void Compress(this BinaryTree node, string prefix, IList<char> traveled, IList<string> compressed)
        {
            var copy = new List<char>(traveled);
            traveled.Add(node.Value);

            if (node.EndOfWord)
            {
                compressed.Add(prefix + new string(traveled.ToArray()));
            }

            if (node.LeftChild != null)
            {
                node.LeftChild.Compress(prefix, traveled, compressed);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.Compress(prefix, copy, compressed);
            }
        }

        public static bool TryGoPath(this BinaryTree node, string path, out BinaryTree leaf)
        {
            var child = node.LeftChild;
            while (child != null)
            {
                if (child.Value.Equals(path[0]))
                {
                    break;
                }
                child = child.RightSibling;
            }
            if (child != null)
            {
                if (path.Length == 1)
                {
                    leaf = child;
                    return true;
                }
                return TryGoPath(child, path.Substring(1), out leaf);
            }
            leaf = null;
            return false;
        }
    }
}