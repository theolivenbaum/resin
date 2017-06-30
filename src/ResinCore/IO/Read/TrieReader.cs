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
        public abstract void Dispose();
        public abstract bool HasMoreSegments();
        public abstract void GoToNextSegment();

        protected LcrsNode LastRead;
        protected LcrsNode Replay;

        protected TrieReader()
        {
            LastRead = LcrsNode.MinValue;
            Replay = LcrsNode.MinValue;
        }

        public IEnumerable<Word> IsWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            while (true)
            {
                LcrsNode node;
                if (TryFindDepthFirst(word, out node))
                {
                    if (node.PostingsAddress == null)
                        throw new InvalidOperationException(
                            "cannot create word without postings address");

                    if (node.EndOfWord)
                        yield return new Word(word, 1, node.PostingsAddress);
                }

                if (HasMoreSegments())
                {
                    GoToNextSegment();
                }
                else
                {
                    break;
                }
            }
        }

        public IEnumerable<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var words = new List<Word>();

            while (true)
            {
                LcrsNode node;

                if (TryFindDepthFirst(prefix, out node))
                {
                    DepthFirst(prefix, new List<char>(), words, prefix.Length - 1);
                }

                if (HasMoreSegments())
                {
                    GoToNextSegment();
                }
                else
                {
                    break;
                }
            }

            return words;
        }

        public IEnumerable<Word> Near(string word, int maxEdits, IDistanceResolver distanceResolver = null)
        {
            if (distanceResolver == null) distanceResolver = new Levenshtein();

            var words = new List<Word>();

            while (true)
            {
                LcrsNode node;

                var prefix = word[0].ToString();

                if (TryFindDepthFirst(prefix, out node))
                {
                    if (node.EndOfWord && distanceResolver.Distance(word, prefix) <= maxEdits)
                    {
                        words.Add(new Word(word[0].ToString()));
                    }

                    WithinEditDistanceDepthFirst(
                        word, word[0].ToString(), words, 1, maxEdits, distanceResolver);
                }

                if (HasMoreSegments())
                {
                    GoToNextSegment();
                }
                else
                {
                    break;
                }
            }

            return words;
        }

        public IEnumerable<Word> WithinRange(string lowerBound, string upperBound)
        {
            if (string.IsNullOrWhiteSpace(lowerBound) &&
                (string.IsNullOrWhiteSpace(upperBound))) throw new ArgumentException("Bounds are unspecified");

            var words = new List<Word>();

            LcrsNode node;

            if (TryFindDepthFirst(lowerBound, out node, greaterThan:true))
            {
                DepthFirst(lowerBound, new List<char>(), words, lowerBound.Length - 1);
            }

            DepthFirst(string.Empty, new List<char>(), words, -1, upperBound);

            return words;
        }

        private void WithinEditDistanceDepthFirst(string word, string state, IList<Word> words, int depth, int maxErrors, IDistanceResolver distanceResolver, bool stop = false)
        {
            var reachedMin = maxErrors == 0 || depth >= word.Length - 1 - maxErrors;
            var reachedDepth = depth >= word.Length - 1;
            var reachedMax = depth >= word.Length + maxErrors;

            var node = Step();

            if (node == LcrsNode.MinValue)
            {
                return;
            }
            else if (node.Value == Serializer.SegmentDelimiter)
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
                    var edits = distanceResolver.Distance(word, test);

                    if (edits <= maxErrors)
                    {
                        if (node.EndOfWord)
                        {
                            words.Add(new Word(test, 1, node.PostingsAddress));
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
                    WithinEditDistanceDepthFirst(word, test, words, depth + 1, maxErrors, distanceResolver, stop);
                }

                // Go right (wide)
                if (node.HaveSibling)
                {
                    WithinEditDistanceDepthFirst(word, state, words, depth, maxErrors, distanceResolver);
                }
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<Word> words, int depth, string upperBound = null)
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
                    if (node.PostingsAddress == null)
                        throw new InvalidOperationException(
                            "cannot create word without postings address");

                    var word = prefix + new string(path.ToArray());

                    if (upperBound == null ||
                       (word.Length <= upperBound.Length && node.Value <= upperBound[depth + 1]) ||
                        word.Length > upperBound.Length)
                    {
                        words.Add(new Word(word, 1, node.PostingsAddress));
                    }
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
                DepthFirst(prefix, siblingState.Item2, words, siblingState.Item1, upperBound);
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
        
        private bool TryFindDepthFirst(string path, out LcrsNode node, bool greaterThan = false)
        {
            var currentDepth = 0;

            node = Step();

            while (node != LcrsNode.MinValue)
            {
                if (node.Value == Serializer.SegmentDelimiter)
                {
                    break;
                }

                if (node.Depth != currentDepth)
                {
                    Skip(node.Weight - 1);
                    node = Step();
                }

                if (node == LcrsNode.MinValue)
                {
                    return false;
                }
                else if (node.Value == Serializer.SegmentDelimiter)
                {
                    break;
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

        private void Rewind()
        {
            Replay = LastRead;
        }
    }
}