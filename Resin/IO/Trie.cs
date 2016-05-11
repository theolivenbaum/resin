using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public class Trie
    {
        private readonly int _depth;
        private readonly char _value;
        private bool _eow;
        private readonly Dictionary<char, Trie> _nodes;

        public char Val { get { return _value; } }
        public int Depth { get { return _depth; } }
        public bool Eow { get { return _eow; } }
        public Dictionary<char, Trie> Nodes { get { return _nodes; } } 

        //public IEnumerable<Trie> EndOfWords()
        //{
        //    if (_eow) yield return this;
        //    foreach (var word in _nodes.Values.SelectMany(c => c.EndOfWords()))
        //    {
        //        yield return word;
        //    }
        //}

        public Trie()
        {
            _nodes = new Dictionary<char, Trie>();
        }

        public Trie(IEnumerable<string> words) : this()
        {
            if (words == null) throw new ArgumentNullException("words");

            foreach (var word in words)
            {
                Add(word);
            }
        }

        private Trie(IEnumerable<char> text, int depth) : this()
        {
            if (text == null) throw new ArgumentNullException("text");

            var list = text.ToArray();

            if(list.Length == 0) throw new ArgumentOutOfRangeException("text");
            
            _value = list[0];
            _depth = depth;

            if (list.Length > 1)
            {
                var overflow = list.Skip(1).ToArray();
                if (overflow.Length > 0)
                {
                    Add(overflow, depth+1);
                }
            }
            else
            {
                _eow = true;
            }
        }


        public Trie(char val, int depth, bool eow)
        {
            _value = val;
            _depth = depth;
            _eow = eow;
        }

        private bool TryResolveChild(char c, TrieReader reader, out Trie trie)
        {
            if (reader == null)
            {
                return _nodes.TryGetValue(c, out trie);
            }
            if (!_nodes.TryGetValue(c, out trie))
            {
                if (reader.TryStep(out trie))
                {
                    _nodes.Add(c, trie);
                    return true;
                }
            }
            return false;
        }

        private IEnumerable<Trie> ResolveChildren(TrieReader reader)
        {
            Trie trie;
            while (reader.TryStep(out trie))
            {
                yield return trie;
            }
        }

        public IEnumerable<string> Similar(string word, int edits, TrieReader reader = null)
        {
            var words = new List<Word>();
            SimScan(word, word, edits, words, reader);
            return words.OrderBy(w => w.Distance).Select(w => w.Value);
        }

        private void SimScan(string word, string state, int edits, IList<Word> words, TrieReader reader)
        {
            foreach (var child in ResolveChildren(reader))
            {
                var tmp = state.ReplaceAt(child.Depth, child.Val);
                if (Levenshtein.Distance(word, tmp) <= edits)
                {
                    if (child.Eow)
                    {
                        var potential = tmp.Substring(0, child.Depth + 1);
                        var distance = Levenshtein.Distance(word, potential);
                        if (distance <= edits) words.Add(new Word { Value = potential, Distance = distance });
                    }
                    child.SimScan(word, tmp, edits, words, reader);
                }
            }
        }

        public bool ContainsToken(string token, TrieReader reader = null)
        {
            var nodes = new List<char>();
            Trie child;
            if (TryResolveChild(token[0], reader, out child))
            {
                child.ExactScan(token, nodes, reader);
            }
            if (nodes.Count > 0) return true;
            return false;
        }

        private void ExactScan(string prefix, List<char> chars, TrieReader reader)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination.
                if (_eow)
                {
                    chars.Add(_value);
                }
            }
            else if (prefix[0] == _value)
            {
                Trie child;
                if (TryResolveChild(prefix[1], reader, out child))
                {
                    child.ExactScan(prefix.Substring(1), chars, reader);
                }
            }
        }

        public IEnumerable<string> Prefixed(string prefix, TrieReader reader = null)
        {
            var words = new List<List<char>>();
            Trie child;
            if (TryResolveChild(prefix[0], reader, out child))
            {
                child.PrefixScan(new List<char>(prefix), prefix, words, reader);
            }
            return words.Select(s=>new string(s.ToArray()));
        }

        public void PrefixScan(List<char> state, string prefix, List<List<char>> words, TrieReader reader)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination. Find words derived from this node.
                if (_eow) words.Add(state);
                foreach (var node in ResolveChildren(reader))
                {
                    var newState = new List<char>(state.Count+1);
                    foreach(var c in state) newState.Add(c);
                    newState.Add(node.Val);
                    node.PrefixScan(newState, new string(new[] { node.Val }), words, reader);
                }
            }
            else if (prefix[0] == _value)
            {
                Trie child;
                if (TryResolveChild(prefix[1], reader, out child))
                {
                    child.PrefixScan(state, prefix.Substring(1), words, reader);
                }
            }
        }

        public void Add(IEnumerable<char> word, int depth = 0)
        {
            if (word == null) throw new ArgumentNullException("word");
            var list = word.ToArray();
            if (list.Length == 0) throw new ArgumentOutOfRangeException("word");

            Trie child;
            if (!_nodes.TryGetValue(list[0], out child))
            {
                child = new Trie(list, depth);
                _nodes.Add(list[0], child);
            }
            else
            {
                child.Append(list, depth);
            }
        }

        private void Append(IEnumerable<char> text, int depth)
        {
            if (text == null) throw new ArgumentNullException("text");
            var list = text.ToArray();
            if (list[0] != _value) throw new ArgumentOutOfRangeException("text");

            var overflow = list.Skip(1).ToArray();
            if (overflow.Length > 0)
            {
                Add(overflow, depth+1);
            }
            else
            {
                _eow = true;
            }
        }

        public void Remove(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            Trie child;
            if (_nodes.TryGetValue(word[0], out child))
            {
                if (child._nodes.Count == 0)
                {
                    _nodes.Remove(child._value);
                }
                else
                {
                    child._eow = false;
                }
                if (word.Length > 1)
                {
                    child.Remove(word.Substring(1));
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}:{2}", _value, _depth, _eow);
        }
    }

    public struct Word
    {
        public string Value;
        public int Distance;
    }
}