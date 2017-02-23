using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Analysis;

namespace Resin.IO.Read
{
    public static class InMemoryTrieReader
    {
        public static bool HasWord(this LcrsTrie node, string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("path");

            LcrsTrie child;
            if (node.TryFindPath(word, out child))
            {
                return child.EndOfWord;
            }
            return false;
        }

        public static IEnumerable<Word> StartsWith(this LcrsTrie node, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("traveled");

            var compressed = new List<Word>();
            
            LcrsTrie child;
            if (node.TryFindPath(prefix, out child))
            {
                child.LeftChild.DepthFirst(prefix, new List<char>(), compressed);
            }
            
            return compressed;
        }

        public static IEnumerable<Word> Near(this LcrsTrie node, string word, int edits)
        {
            var compressed = new List<Word>();
            if (node.LeftChild != null)
            {
                node.LeftChild.WithinEditDistanceDepthFirst(word, new string(new char[word.Length]), compressed, 0, edits);
            }
            return compressed.OrderBy(w => w.Distance);
        }

        private static void WithinEditDistanceDepthFirst(this LcrsTrie node, string word, string state, IList<Word> compressed, int depth, int maxEdits)
        {
            var childIndex = depth + 1;
            string test;
            
            if (depth == state.Length)
            {
                test = state + node.Value;
            }
            else
            {
                test = new string(state.ReplaceOrAppend(depth, node.Value).Where(c=>c!=Char.MinValue).ToArray());
            }

            var edits = Levenshtein.Distance(word, test);

            if (edits <= maxEdits)
            {
                if (node.EndOfWord)
                {
                    compressed.Add(new Word(test){ Distance = edits });
                }
            }

            if (edits <= maxEdits || test.Length < word.Length)
            {
                if (node.LeftChild != null)
                {
                    node.LeftChild.WithinEditDistanceDepthFirst(word, test, compressed, childIndex, maxEdits);
                }

                if (node.RightSibling != null)
                {
                    node.RightSibling.WithinEditDistanceDepthFirst(word, test, compressed, depth, maxEdits);
                }
            }
        }

        private static void DepthFirst(this LcrsTrie node, string traveled, IList<char> state, IList<Word> compressed)
        {
            var copy = new List<char>(state);
            state.Add(node.Value);

            if (node.EndOfWord)
            {
                compressed.Add(new Word(traveled + new string(state.ToArray())));
            }

            if (node.LeftChild != null)
            {
                node.LeftChild.DepthFirst(traveled, state, compressed);
            }

            if (node.RightSibling != null)
            {
                node.RightSibling.DepthFirst(traveled, copy, compressed);
            }
        }

        public static bool TryFindPath(this LcrsTrie node, string path, out LcrsTrie leaf)
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