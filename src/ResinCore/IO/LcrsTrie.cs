using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Resin.Analysis;
using Resin.Sys;

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

        public void Add(string word)
        {
            Add(word,new DocumentPosting(-1, 1));
        }

        public void Add(string word, params DocumentPosting[] postings)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            var key = word[0];
            var eow = word.Length == 1;

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
                node.Add(word.Substring(1), postings);
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

        public IEnumerable<Word> GreaterThan(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            throw new NotImplementedException();
        }

        public IEnumerable<Word> LessThan(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            throw new NotImplementedException();
        }

        public IEnumerable<Word> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            var words = new List<Word>();

            LcrsTrie child;
            if (TryFindPath(prefix, out child) && child.LeftChild != null)
            {
                child.LeftChild.DepthFirst(prefix, new List<char>(), words);
            }

            return words;
        }
        
        public IEnumerable<Word> Near(string word, int maxEdits, IDistanceResolver distanceResolver = null)
        {
            if (distanceResolver == null) distanceResolver = new Levenshtein();

             var compressed = new List<Word>();
            if (LeftChild != null)
            {
                LeftChild.WithinEditDistanceDepthFirst(word, string.Empty, compressed, 0, maxEdits, distanceResolver);
            }
            return compressed;
        }

        private void WithinEditDistanceDepthFirst(string word, string state, List<Word> compressed, int depth, int maxEdits, IDistanceResolver distanceResolver)
        {
            string test;

            if (depth == state.Length)
            {
                test = state + Value;
            }
            else
            {
                test = new string(state.ReplaceOrAppend(depth, Value).ToArray());
            }

            var edits = distanceResolver.Distance(word, test);

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
                    LeftChild.WithinEditDistanceDepthFirst(word, test, compressed, depth+1, maxEdits, distanceResolver);
                }

                if (RightSibling != null)
                {
                    RightSibling.WithinEditDistanceDepthFirst(word, test, compressed, depth, maxEdits, distanceResolver);
                }
            }
        }

        private void DepthFirst(string traveled, IList<char> state, IList<Word> words)
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
                words.Add(word);
            }

            if (LeftChild != null)
            {
                LeftChild.DepthFirst(traveled, state, words);
            }

            if (RightSibling != null)
            {
                RightSibling.DepthFirst(traveled, copy, words);
            }
        }

        public bool TryFindPath(string path, out LcrsTrie leaf)
        {
            var node = LeftChild;
            var c = path[0];
            var index = 0;

            // Find path[index] in a binary (left-right) tree.
            // Stop when destination has been reached.

            while (true)
            {
                if (node == null) break;

                if (node.Value.Equals(path[index]))
                {
                    if (index + 1 == path.Length)
                    {
                        // destination has been reached

                        leaf = node;
                        return true;
                    }
                    else
                    {
                        // go deep when you've found c

                        index++;
                        node = node.LeftChild;
                    }
                }
                else
                {
                    // go right when you are looking for c

                    node = node.RightSibling;
                }
            }
            leaf = null;
            return false;
        }
    }
}