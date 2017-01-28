using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO
{
    public class FileTreeScanner
    {
        private readonly StreamReader _sr;

        public FileTreeScanner(StreamReader sr)
        {
            _sr = sr;
        }

        public bool HasWord(BinaryTree node, string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("path");

            BinaryTree child;
            if (node.TryFindPath(word, out child))
            {
                return child.EndOfWord;
            }
            return false;
        }

        public static IList<string> StartsWith(BinaryTree node, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var compressed = new List<string>();
            
            BinaryTree child;
            if (node.TryFindPath(prefix, out child))
            {
                child.LeftChild.Traverse(prefix, new List<char>(), compressed);
            }
            
            return compressed;
        }

        public static IList<string> Near(BinaryTree node, string word, int edits)
        {
            var compressed = new List<Word>();
            if (node.LeftChild != null)
            {
                node.LeftChild.Traverse(word, new string(word.ToCharArray()), compressed, 0, edits);
            }
            return compressed.OrderBy(w => w.Distance).Select(w => w.Value).ToList();
        }

        public static void Traverse(BinaryTree node, string word, string state, IList<Word> compressed, int index, int maxEdits)
        {
            var childIndex = index + 1;
            string test;

            if (index > 0)
            {
                test = state.Substring(state.Length + index);
            }
            else if (index == state.Length)
            {
                test = state + node.Value;
            }
            else
            {
                test = state.ReplaceOrAppendToString(index, node.Value);
            }

            var edits = Levenshtein.Distance(word, test);

            if (edits <= maxEdits)
            {
                if (node.EndOfWord)
                {
                    compressed.Add(new Word { Value = test, Distance = edits });
                }

                if (node.LeftChild != null)
                {
                    node.LeftChild.Traverse(word, test, compressed, childIndex, maxEdits);
                }

                if (node.RightSibling != null)
                {
                    node.RightSibling.Traverse(word, test, compressed, index, maxEdits);
                }
            }
            else
            {
                if (node.LeftChild != null)
                {
                    node.LeftChild.Traverse(word, test, compressed, childIndex, maxEdits);
                }

                if (node.RightSibling != null)
                {
                    node.RightSibling.Traverse(word, test, compressed, index, maxEdits);
                }
            }
        }

        public static void Traverse(BinaryTree node, string prefix, IList<char> traveled, IList<string> compressed)
        {
            var copy = new List<char>(traveled);
            traveled.Add(node.Value);

            if (node.EndOfWord)
            {
                compressed.Add(prefix + new string(traveled.ToArray()));
            }

            if (node.LeftChild != null)
            {
                node.LeftChild.Traverse(prefix, traveled, compressed);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.Traverse(prefix, copy, compressed);
            }
        }

        public static bool TryFindPath(BinaryTree node, string path, out BinaryTree leaf)
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
                return TryFindPath(child, path.Substring(1), out leaf);
            }
            leaf = null;
            return false;
        }
    }
}