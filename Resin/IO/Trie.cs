using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    [Serializable]
    public class Trie : CompressedFileBase<Trie>
    {
        private readonly char _value;

        private bool _eow;

        protected readonly Dictionary<char, Trie> Nodes;

        public char Val { get { return _value; } }

        public Trie()
        {
            Nodes = new Dictionary<char, Trie>();
        }

        public Trie(IEnumerable<string> words) : this()
        {
            if (words == null) throw new ArgumentNullException("words");

            foreach (var word in words)
            {
                Add(word);
            }
        }

        private Trie(IEnumerable<char> text) : this()
        {
            if (text == null) throw new ArgumentNullException("text");
            var list = text.ToArray();
            if(list.Length == 0) throw new ArgumentOutOfRangeException("text");
            _value = list[0];

            if (list.Length > 1)
            {
                var overflow = list.Skip(1).ToArray();
                if (overflow.Length > 0)
                {
                    Add(overflow);
                }
            }
            else
            {
                _eow = true;
            }
        }

        public IEnumerable<Trie> Children()
        {
            return Nodes.Values;
        }
        
        protected virtual bool TryResolveChild(char c, out Trie trie)
        {
            return Nodes.TryGetValue(c, out trie);
        }

        protected virtual IEnumerable<Trie> ResolveChildren()
        {
            return Nodes.Values;
        } 
        
        public IEnumerable<string> Similar(string word, int edits)
        {
            var words = new List<Word>();
            SimScan(word, word, edits, 0, words);
            return words.OrderBy(w=>w.Distance).Select(w=>w.Value);
        }

        private struct Word
        {
            public string Value;
            public int Distance;
        }

        private void SimScan(string word, string state, int edits, int index, IList<Word> words)
        {
            var childIndex = index + 1;
            foreach (var child in Nodes.Values)
            {
                var tmp = index == state.Length ? state + child._value : state.ReplaceAt(index, child._value);
                if (Levenshtein.Distance(word, tmp) <= edits)
                {
                    if (child._eow)
                    {
                        var potential = tmp.Substring(0, childIndex);
                        var distance = Levenshtein.Distance(word, potential);
                        if (distance <= edits) words.Add(new Word{Value = potential, Distance = distance});
                    }
                    child.SimScan(word, tmp, edits, childIndex, words);  
                }
            }
        }

        public bool ContainsToken(string token)
        {
            var nodes = new List<char>();
            Trie child;
            if (TryResolveChild(token[0], out child))
            {
                child.ExactScan(token, nodes);
            }
            if (nodes.Count > 0) return true;
            return false;
        }

        private void ExactScan(string prefix, List<char> chars)
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
                if (Nodes.TryGetValue(prefix[1], out child))
                {
                    child.ExactScan(prefix.Substring(1), chars);
                }
            }
        }

        public IEnumerable<string> Prefixed(string prefix)
        {
            var words = new List<List<char>>();
            Trie child;
            if (TryResolveChild(prefix[0], out child))
            {
                child.PrefixScan(new List<char>(prefix), prefix, words);
            }
            return words.Select(s=>new string(s.ToArray()));
        }

        private void PrefixScan(List<char> state, string prefix, List<List<char>> words)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination. Find words derived from this node.
                if (_eow) words.Add(state);
                foreach (var node in ResolveChildren())
                {
                    var newState = new List<char>(state.Count+1);
                    foreach(var c in state) newState.Add(c);
                    newState.Add(node._value);
                    node.PrefixScan(newState, new string(new[] { node._value }), words);
                }
            }
            else if (prefix[0] == _value)
            {
                Trie child;
                if (Nodes.TryGetValue(prefix[1], out child))
                {
                    child.PrefixScan(state, prefix.Substring(1), words);
                }
            }
        }

        public void Add(IEnumerable<char> word)
        {
            if (word == null) throw new ArgumentNullException("word");
            var list = word.ToArray();
            if (list.Length == 0) throw new ArgumentOutOfRangeException("word");

            Trie child;
            if (!Nodes.TryGetValue(list[0], out child))
            {
                child = new Trie(list);
                Nodes.Add(list[0], child);
            }
            else
            {
                child.Append(list);
            }
        }

        private void Append(IEnumerable<char> text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var list = text.ToArray();
            if (list[0] != _value) throw new ArgumentOutOfRangeException("text");

            var overflow = list.Skip(1).ToArray();
            if (overflow.Length > 0)
            {
                Add(overflow);
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
            if (Nodes.TryGetValue(word[0], out child))
            {
                if (child.Nodes.Count == 0)
                {
                    Nodes.Remove(child._value);
                }
                else
                {
                    child._eow = false;
                }
                if (word.Length > 1) child.Remove(word.Substring(1));
            }
        }
    }
}