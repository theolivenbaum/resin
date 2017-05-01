using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Resin.Analysis;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsTrie : ITrieReader
    {
        public LcrsTrie RightSibling { get; set; }
        public LcrsTrie LeftChild { get; set; }
        public BlockInfo PostingsAddress { get; set; }
        public List<DocumentPosting> Postings { get; set; }
        public int WordCount { get; private set; }
        public char Value { get; private set; }
        public bool EndOfWord { get; private set; }

        public int Weight
        {
            get
            {
                return 1 
                    + (LeftChild == null ? 0 : LeftChild.Weight) 
                    + (RightSibling == null ? 0 : RightSibling.Weight);
            }
        }

        public LcrsTrie():this('\0', false)
        {
        }

        public LcrsTrie(char value, bool endOfWord)
        {
            Value = value;
            EndOfWord = endOfWord;

            if (EndOfWord) WordCount++;
        }

        public void Merge(LcrsTrie other)
        {
            var words = new List<Word>();

            other.LeftChild.DepthFirst(string.Empty, new List<char>(), words);

            var nodes = other.LeftChild.EndOfWordNodes().ToArray();

            for (int index = 0;index < nodes.Length; index++)
            {
                Add(words[index].Value, nodes[index].Postings.ToArray());
            }
        }

        public IEnumerable<Word> Words()
        {
            var words = new List<Word>();

            DepthFirst(string.Empty, new List<char>(), words);

            return words;
        }

        public IEnumerable<LcrsTrie> EndOfWordNodes()
        {
            if (EndOfWord)
            {
                yield return this;
            }

            if (LeftChild != null)
            {
                foreach (var node in LeftChild.EndOfWordNodes())
                {
                    yield return node;
                }
            }

            if (RightSibling != null)
            {
                foreach (var node in RightSibling.EndOfWordNodes())
                {
                    yield return node;
                }
            }
        }

        public void Add(string path)
        {
            Add(path,new DocumentPosting(-1, 1));
        }

        public void Add(string path, params DocumentPosting[] postings)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("word");

            var key = path[0];
            var eow = path.Length == 1;

            LcrsTrie node;
            if (!TryGetChild(key, out node))
            {
                node = new LcrsTrie(key, eow);

                if (LeftChild == null)
                {
                    LeftChild = node;

                }
                else
                {
                    // place new node in lexical order

                    if (LeftChild.Value > node.Value)
                    {
                        var tmp = LeftChild;
                        LeftChild = node;
                        node.RightSibling = tmp;
                    }
                    else
                    {
                        var sibling = LeftChild;

                        while (true)
                        {
                            if (sibling.Value < node.Value && (sibling.RightSibling == null || sibling.RightSibling.Value > node.Value))
                            {
                                break;
                            }
                            sibling = sibling.RightSibling;
                        }
                        var rightSibling = sibling.RightSibling;
                        sibling.RightSibling = node;
                        node.RightSibling = rightSibling;
                    }
                }
            }
            else if (eow)
            {
                node.EndOfWord = true;
                node.WordCount++;
            }

            if (eow)
            {
                if (node.Postings == null)
                {
                    node.Postings = new List<DocumentPosting>();
                }
                foreach (var posting in postings)
                {
                    node.Postings.Add(posting);
                }
            }
            else
            {
                node.Add(path.Substring(1), postings);
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

                if (c < node.Value) break;

                node = node.RightSibling;
            }

            node = null;
            return false;
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

        public bool HasWord(string word, out Word found)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            LcrsTrie node;
            if (TryFindPath(word, out node))
            {
                if (node.WordCount == 0)
                {
                    throw new InvalidOperationException("WordCount");
                }
                found = new Word(word, node.WordCount, node.PostingsAddress, node.Postings);
                return node.EndOfWord;
            }
            found = Word.MinValue;
            return false;
        }

        public IEnumerable<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var compressed = new List<Word>();

            LcrsTrie child;
            if (TryFindPath(prefix, out child))
            {
                child.LeftChild.DepthFirst(prefix, new List<char>(), compressed);
            }

            return compressed;
        }

        public IEnumerable<Word> Near(string word, int maxEdits)
        {
            var compressed = new List<Word>();
            if (LeftChild != null)
            {
                LeftChild.WithinEditDistanceDepthFirst(word, new string(new char[word.Length]), compressed, 0, maxEdits);
            }
            return compressed;
        }

        private void WithinEditDistanceDepthFirst(string word, string state, List<Word> compressed, int depth, int maxEdits)
        {
            var childIndex = depth + 1;
            string test;

            if (depth == state.Length)
            {
                test = state + Value;
            }
            else
            {
                test = new string(state.ReplaceOrAppend(depth, Value).Where(c => c != Char.MinValue).ToArray());
            }

            var edits = Levenshtein.Distance(word, test);

            if (edits <= maxEdits)
            {
                if (EndOfWord)
                {
                    compressed.Add(new Word(test, WordCount, PostingsAddress, Postings));
                }
            }

            if (edits <= maxEdits || test.Length < word.Length)
            {
                if (LeftChild != null)
                {
                    LeftChild.WithinEditDistanceDepthFirst(word, test, compressed, childIndex, maxEdits);
                }

                if (RightSibling != null)
                {
                    RightSibling.WithinEditDistanceDepthFirst(word, test, compressed, depth, maxEdits);
                }
            }
        }

        private void DepthFirst(string traveled, IList<char> state, IList<Word> compressed)
        {
            var copy = new List<char>(state);

            if (Value != char.MinValue)
            {
                state.Add(Value);
            }

            if (EndOfWord)
            {
                var value = traveled + new string(state.ToArray());
                var word = new Word(value, WordCount, PostingsAddress, Postings);
                compressed.Add(word);
            }

            if (LeftChild != null)
            {
                LeftChild.DepthFirst(traveled, state, compressed);
            }

            if (RightSibling != null)
            {
                RightSibling.DepthFirst(traveled, copy, compressed);
            }
        }

        public bool TryFindPath(string path, out LcrsTrie leaf)
        {
            var child = LeftChild;
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
                return child.TryFindPath(path.Substring(1), out leaf);
            }
            leaf = null;
            return false;
        }
    }
}