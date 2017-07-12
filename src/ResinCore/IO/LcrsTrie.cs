using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Resin.Analysis;
using Resin.Sys;
using System.Text;
using StreamIndex;
using DocumentTable;

namespace Resin.IO
{
    [DebuggerDisplay("{Value} {EndOfWord}")]
    public class LcrsTrie : ITrieReader
    {
        public LcrsTrie RightSibling { get; set; }
        public LcrsTrie LeftChild { get; set; }
        public BlockInfo PostingsAddress { get; set; }
        public List<DocumentPosting> Postings { get; set; }
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
        }

        public void Merge(LcrsTrie other)
        {
            var words = new List<Word>();

            other.LeftChild.DepthFirst(string.Empty, new List<char>(), words);

            var nodes = other.LeftChild.EndOfWordNodes().ToArray();

            for (int index = 0;index < nodes.Length; index++)
            {
                Add(words[index].Value, 0, nodes[index].Postings.ToArray());
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

        public IEnumerable<LcrsTrie> AllNodesDepthFirst()
        {
            yield return this;

            if (LeftChild != null)
            {
                foreach (var node in LeftChild.AllNodesDepthFirst())
                {
                    yield return node;
                }
            }

            if (RightSibling != null)
            {
                foreach (var node in RightSibling.AllNodesDepthFirst())
                {
                    yield return node;
                }
            }
        }

        public void Add(string word)
        {
            Add(word, 0, new DocumentPosting(-1, 1));
        }

        public void Add(string word, int index, params DocumentPosting[] postings)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            if (index == word.Length) return;

            var key = word[index];
            var eow = word.Length == index + 1;

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
                            if (sibling.Value < node.Value && (sibling.RightSibling == null || 
                                sibling.RightSibling.Value > node.Value))
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

            if (eow)
            {
                node.EndOfWord = true;

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
                node.Add(word, index + 1, postings);
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

        public IList<Word> IsWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            var words = new List<Word>();

            LcrsTrie node;
            if (TryFindPath(word, out node))
            {
                if (node.EndOfWord)
                {
                    words.Add(new Word(
                        word, node.PostingsAddress, node.Postings));
                }
            }

            return words;
        }

        public bool IsWord(char[] word)
        {
            if (word.Length == 0) throw new ArgumentException("word");

            LcrsTrie node;
            if (TryFindPath(word, out node))
            {
                if (node.EndOfWord)
                {
                    return true;
                }
            }

            return false;
        }

        public IList<Word> Range(string lowerBound, string upperBound)
        {
            if (string.IsNullOrWhiteSpace(lowerBound) &&
                (string.IsNullOrWhiteSpace(upperBound))) throw new ArgumentException("Bounds are unspecified");

            throw new NotImplementedException();

            //TODO: implement bounded DepthFirst
        }

        public IList<Word> StartsWith(string prefix)
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
        
        public IList<Word> SemanticallyNear(string word, int maxEdits, IDistanceResolver distanceResolver = null)
        {
            if (distanceResolver == null) distanceResolver = new LevenshteinDistanceResolver(word, maxEdits);

             var compressed = new List<Word>();
            if (LeftChild != null)
            {
                LeftChild.WithinEditDistanceDepthFirst(word, string.Empty, compressed, 0, maxEdits, distanceResolver);
            }
            return compressed;
        }

        private void WithinEditDistanceDepthFirst(
            string word, string state, List<Word> words, int depth, int maxEdits, IDistanceResolver distanceResolver, bool stop = false)
        {
            var reachedMin = maxEdits == 0 || depth >= word.Length - 1 - maxEdits;
            var reachedDepth = depth >= word.Length - 1;
            var reachedMax = depth >= word.Length + maxEdits;

            if (!reachedMax && !stop)
            {
                string test;

                if (depth == state.Length)
                {
                    test = state + Value;
                }
                else
                {
                    test = state.ReplaceOrAppend(depth, Value);
                }

                if (reachedMin)
                {
                    if (distanceResolver.IsValid(Value, depth))
                    {
                        if (EndOfWord)
                        {
                            if(distanceResolver.GetDistance(word, test) <= maxEdits)
                            {
                                words.Add(new Word(test, PostingsAddress));
                            }
                        }
                    }
                    else
                    {
                        stop = true;
                    }
                }
                else
                {
                    distanceResolver.Put(Value, depth);
                }

                // Go left (deep)
                if (LeftChild != null)
                {
                    LeftChild.WithinEditDistanceDepthFirst(word, test, words, depth + 1, maxEdits, distanceResolver, stop);
                }

                // Go right (wide)
                if (RightSibling != null)
                {
                    RightSibling.WithinEditDistanceDepthFirst(word, state, words, depth, maxEdits, distanceResolver);
                }
            }  
        }

        private void DepthFirst(string traveled, IList<char> state, IList<Word> words, bool surpassedLbound = false, string lbound = null, string ubound = null)
        {
            var copy = new List<char>(state);

            if (Value != char.MinValue)
            {
                state.Add(Value);
            }

            if (EndOfWord)
            {
                var word = traveled + new string(state.ToArray());

                if ((surpassedLbound && word != lbound) || word != ubound)
                {
                    
                    words.Add(new Word(word, PostingsAddress, Postings));
                }
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

        public bool TryFindPath(char[] path, out LcrsTrie leaf)
        {
            var node = LeftChild;
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

        public string Visualize()
        {
            StringBuilder output = new StringBuilder();
            Visualize(LeftChild, output, 0);
            return output.ToString();
        }

        public LcrsTrie Balance()
        {
            var nodes = AllNodesDepthFirst().ToArray();

            return Balance(nodes, 0, nodes.Length - 1);
        }

        private LcrsTrie Balance(LcrsTrie[] arr, int start, int end)
        {
            // this will distort the tree
            // TODO: balance a sorted list of strings instead of a list of nodes

            if (start > end)
            {
                return null;
            }

            int mid = (start + end) / 2;

            LcrsTrie node = arr[mid];

            node.LeftChild = Balance(arr, start, mid - 1);

            node.RightSibling = Balance(arr, mid + 1, end);

            return node;
        }

        private void Visualize(LcrsTrie node, StringBuilder output, int depth)
        {
            if (node == null) return;

            output.Append('\t', depth);
            output.Append(node.Value.ToString() + " ");
            output.AppendLine();

            Visualize(node.LeftChild, output, depth + 1);
            Visualize(node.RightSibling, output, depth);
        }

        public bool HasMoreSegments()
        {
            return false;
        }

        public void Dispose()
        {
        }
    }
}