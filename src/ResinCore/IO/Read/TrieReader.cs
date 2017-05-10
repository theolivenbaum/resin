using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Analysis;
using Resin.Sys;

namespace Resin.IO.Read
{
    public abstract class TrieReader : ITrieReader
    {
        protected abstract LcrsNode Step();
        protected abstract void Skip(int count);

        protected LcrsNode LastRead;
        protected LcrsNode Replay;

        protected TrieReader()
        {
            LastRead = LcrsNode.MinValue;
            Replay = LcrsNode.MinValue;
        }

        public bool HasWord(string word, out Word found)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            LcrsNode node;
            if (TryFindDepthFirst(word, 0, out node))
            {
                found = new Word(word, 1, node.PostingsAddress);
                return node.EndOfWord;
            }
            found = Word.MinValue;
            return false;
        }

        public IEnumerable<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var compressed = new List<Word>();
            LcrsNode node;

            if (TryFindDepthFirst(prefix, 0, out node))
            {
                DepthFirst(prefix, new List<char>(), compressed, prefix.Length - 1);
            }

            return compressed;
        }

        public IEnumerable<Word> Near(string word, int maxEdits, IDistanceResolver distanceResolver = null)
        {
            if (distanceResolver == null) distanceResolver = new Levenshtein();

            var compressed = new List<Word>();

            WithinEditDistanceDepthFirst(word, string.Empty, compressed, 0, maxEdits, distanceResolver);

            return compressed;
        }

        private void WithinEditDistanceDepthFirst(string word, string state, IList<Word> compressed, int depth, int maxErrors, IDistanceResolver distanceResolver, bool stop = false)
        {
            var reachedMin = maxErrors == 0 || depth >= word.Length - 1 - maxErrors;
            var reachedDepth = depth >= word.Length - 1;
            var reachedMax = depth >= word.Length + maxErrors;

            var node = Step();

            if (node == LcrsNode.MinValue) return;

            if (reachedMax || stop)
            {
                Skip(node.Weight - 1);
            }
            else
            {
                string test;

                if (depth == state.Length)
                {
                    test = state + node.Value;
                }
                else
                {
                    test = new string(state.ReplaceOrAppend(depth, node.Value).ToArray());
                }

                if (reachedMin)
                {
                    var edits = distanceResolver.Distance(word, test);

                    if (edits <= maxErrors)
                    {
                        if (node.EndOfWord)
                        {
                            compressed.Add(new Word(test, 1, node.PostingsAddress));
                        }
                    }
                    else if (edits > maxErrors && reachedDepth)
                    {
                        stop = true;
                    }
                    else if (reachedDepth)
                    {
                        stop = true;
                    }
                }

                // Go left (deep)
                if (node.HaveChild)
                {
                    WithinEditDistanceDepthFirst(word, test, compressed, depth + 1, maxErrors, distanceResolver, stop);
                }

                // Go right (wide)
                if (node.HaveSibling)
                {
                    WithinEditDistanceDepthFirst(word, state, compressed, depth, maxErrors, distanceResolver);
                }
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<Word> compressed, int depth)
        {
            var node = Step();
            var siblings = new Stack<Tuple<int, IList<char>>>();

            // Go left (deep)
            while (node != LcrsNode.MinValue && node.Depth > depth)
            {
                var copyOfPath = new List<char>(path);

                path.Add(node.Value);

                if (node.EndOfWord)
                {
                    compressed.Add(new Word(prefix + new string(path.ToArray()), 1, node.PostingsAddress));
                }

                if (node.HaveSibling)
                {
                    siblings.Push(new Tuple<int, IList<char>>(depth, copyOfPath));
                }

                depth = node.Depth;
                node = Step();
            }

            Rewind();

            // Go right (wide)
            foreach (var siblingState in siblings)
            {
                DepthFirst(prefix, siblingState.Item2, compressed, siblingState.Item1);
            }
        }

        public LcrsTrie ReadWholeFile()
        {
            var words = new List<Word>();
            DepthFirst(string.Empty, new List<char>(), words, -1);

            var root = new LcrsTrie();

            foreach (var word in words)
            {
                root.Add(word.Value);
            }

            return root.LeftChild;
        }

        private bool TryFindDepthFirst(string path, int currentDepth, out LcrsNode node)
        {
            node = Step();

            if (node != LcrsNode.MinValue && node.Depth != currentDepth)
            {
                Skip(node.Weight-1);
                node = Step();
            }

            if (node != LcrsNode.MinValue)
            {
                if (node.Value == path[currentDepth])
                {
                    if (currentDepth == path.Length - 1)
                    {
                        return true;
                    }
                    // Go left (deep)
                    return TryFindDepthFirst(path, currentDepth + 1, out node);
                }
                // Go right (wide)
                return TryFindDepthFirst(path, currentDepth, out node);
            }

            return false;
        }

        private void Rewind()
        {
            Replay = LastRead;
        }
    }
}