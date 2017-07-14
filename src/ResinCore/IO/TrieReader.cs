using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Analysis;
using Resin.Sys;
using DocumentTable;

namespace Resin.IO.Read
{
    public abstract class TrieReader : ITrieReader
    {
        protected abstract LcrsNode Step();
        protected abstract void Skip(int count);
        public abstract void Dispose();

        protected LcrsNode LastRead;
        protected LcrsNode Replay;

        protected TrieReader()
        {
            LastRead = LcrsNode.MinValue;
            Replay = LcrsNode.MinValue;
        }

        public IList<Word> IsWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            var words = new List<Word>();

            LcrsNode node;
            if (TryFindDepthFirst(word, out node))
            {
                if (node.PostingsAddress == null)
                    throw new InvalidOperationException(
                        "cannot create word without postings address");

                if (node.EndOfWord)
                    words.Add(new Word(word, node.PostingsAddress));
            }

            return words;
        }

        public IList<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var words = new List<Word>();

            LcrsNode node;

            if (TryFindDepthFirst(prefix, out node))
            {
                DepthFirst(prefix, new List<char>(), words, prefix.Length - 1);
            }

            return words;
        }

        public IList<Word> SemanticallyNear(string word, int maxEdits, IDistanceResolver distanceResolver = null)
        {
            if (distanceResolver == null) distanceResolver = new LevenshteinDistanceResolver(word, maxEdits);

            var words = new List<Word>();

            LcrsNode node;

            var prefix = word[0].ToString();

            if (TryFindDepthFirst(prefix, out node))
            {
                if (node.EndOfWord &&
                    distanceResolver.IsValid(node.Value, 0) &&
                    distanceResolver.GetDistance(word, prefix) <= maxEdits)
                {
                    words.Add(new Word(word[0].ToString()));
                }

                WithinEditDistanceDepthFirst(
                    word, word[0].ToString(), words, 1, maxEdits, distanceResolver);
            }

            return words;
        }

        public IList<Word> Range(string lowerBound, string upperBound)
        {
            if (string.IsNullOrWhiteSpace(lowerBound) &&
                (string.IsNullOrWhiteSpace(upperBound))) throw new ArgumentException("Bounds are unspecified");

            var words = new List<Word>();

            DepthFirst(lowerBound, upperBound, new List<char>(), words);

            return words;
        }

        private void WithinEditDistanceDepthFirst(
            string word, string state, IList<Word> words, int depth, int maxEdits, IDistanceResolver distanceResolver, bool stop = false)
        {
            var reachedMin = maxEdits == 0 || depth >= word.Length - 1 - maxEdits;
            var reachedMax = depth >= word.Length + maxEdits;

            var node = Step();

            if (node == LcrsNode.MinValue)
            {
                return;
            }

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
                    test = state.ReplaceOrAppend(depth, node.Value);
                }

                if (reachedMin)
                {
                    if (distanceResolver.IsValid(node.Value, depth))
                    {
                        if (node.EndOfWord)
                        {
                            if (distanceResolver.GetDistance(word, test) <= maxEdits)
                            {
                                words.Add(new Word(test, node.PostingsAddress));
                            }
                        }
                    }
                    else
                    {
                        stop = true;
                    }
                }

                // Go left (deep)
                if (node.HaveChild)
                {
                    WithinEditDistanceDepthFirst(word, test, words, depth + 1, maxEdits, distanceResolver, stop);
                }

                // Go right (wide)
                if (node.HaveSibling)
                {
                    WithinEditDistanceDepthFirst(word, state, words, depth, maxEdits, distanceResolver);
                }
            }
        }

        private void DepthFirst(string lowerBound, string upperBound, IList<char> path, IList<Word> words)
        {
            var siblingState = new Stack<IList<char>>();
            var lastDepth = -1;

            while (true)
            {
                var node = Step();

                if (node == LcrsNode.MinValue) return;

                if (node.Depth <= lastDepth)
                {
                    Rewind();

                    break;
                }

                lastDepth = node.Depth;

                var copyOfPath = new List<char>(path);
                path.Add(node.Value);

                if (node.HaveSibling)
                {
                    siblingState.Push(new List<char>(copyOfPath));
                }

                var test = new string(path.ToArray());
                var testAgainstLo = lowerBound.Substring(0, Math.Min(lowerBound.Length, node.Depth + 1));
                var comparedToLowerBound = test.CompareTo(testAgainstLo);
                var largeEnough = comparedToLowerBound > -1;

                if (!largeEnough)
                {
                    path = copyOfPath;
                    SkipToSibling(node);
                    break;
                }
                else
                {
                    var testAgainstHi = upperBound.Substring(0, Math.Min(upperBound.Length, node.Depth + 1));
                    var comparedToUpperBound = test.CompareTo(testAgainstHi);
                    var tooLarge = comparedToUpperBound > 0;

                    if (tooLarge)
                    {
                        return;
                    }

                    if (node.EndOfWord)
                    {
                        if (node.PostingsAddress == null)
                            throw new InvalidOperationException(
                                "cannot create word without postings address");


                        var word = new string(path.ToArray());

                        words.Add(new Word(word, node.PostingsAddress));
                    }
                }
            }

            // Go right (wide)
            foreach (var state in siblingState)
            {
                DepthFirst(lowerBound, upperBound, state, words);
            }
        }

        private void SkipToSibling(LcrsNode node)
        {
            while (true)
            {
                var test = Step();
                if (test.Depth <= node.Depth)
                {
                    Rewind();
                    break;
                }
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<Word> words, int depth)
        {
            // Go left (deep)
            var node = Step();

            var siblings = new Stack<Tuple<int, IList<char>>>();

            while (node != LcrsNode.MinValue && node.Depth > depth)
            {
                var copyOfPath = new List<char>(path);

                path.Add(node.Value);

                if (node.EndOfWord)
                {
                    if (node.PostingsAddress == null)
                        throw new InvalidOperationException(
                            "cannot create word without postings address");

                    var word = prefix + new string(path.ToArray());

                    words.Add(new Word(word, node.PostingsAddress));
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
                DepthFirst(prefix, siblingState.Item2, words, siblingState.Item1);
            }
        }

        public LcrsTrie ReadWholeFile()
        {
            var words = new List<Word>();

            DepthFirst(string.Empty, new List<char>(), words, -1);

            var root = new LcrsTrie();

            // TODO: assemble trie node by node
            foreach (var word in words)
            {
                root.Add(word.Value);
            }

            return root;
        }
        
        private bool TryFindDepthFirst(string path, out LcrsNode node, bool greaterThan = false)
        {
            var currentDepth = 0;

            node = Step();

            while (node != LcrsNode.MinValue)
            {
                while (node.Depth != currentDepth)
                {
                    Skip(node.Weight - 1);
                    node = Step();
                    if (node == LcrsNode.MinValue)
                    {
                        break;
                    }
                }

                if (node == LcrsNode.MinValue)
                {
                    return false;
                }

                if ((greaterThan && node.Value >= path[currentDepth]) ||
                    (node.Value == path[currentDepth]))
                {
                    if (currentDepth == path.Length - 1)
                    {
                        return true;
                    }

                    // Go left (deep)
                    currentDepth++;
                }

                // Or go right (wide)
                node = Step();
            }
            return false;
        }

        public IEnumerable<Word> Words()
        {
            var words = new List<Word>();

            DepthFirst(string.Empty, new List<char>(), words, -1);

            return words;
        }

        private void Rewind()
        {
            Replay = LastRead;
        }
    }
}