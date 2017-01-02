using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Resin.IO
{
    public struct Word
    {
        public string Value;
        public int Distance;

        public override string ToString()
        {
            return Value;
        }
    }

    public class Trie : IDisposable
    {
        private readonly IDictionary<char, Trie> _nodes;
 
        public char Value { get; protected set; }
        public bool Eow { get; protected set; }

        public IEnumerable<Trie> Nodes
        {
            get { return _nodes.Values; }
        }

        public int Count
        {
            get { return GetCount(); }
        }

        protected virtual int GetCount()
        {
            return _nodes.Count;
        }

        protected virtual IEnumerable<Trie> GetChildren()
        {
            return _nodes.Values;
        }

        public Trie()
        {
            _nodes = new Dictionary<char, Trie>();
        }

        public Trie(IEnumerable<string> words): this()
        {
            if (words == null) throw new ArgumentNullException("words");

            foreach (var word in words)
            {
                Add(word.ToCharArray());
            }
        }

        private Trie(char[] chars): this()
        {
            if (chars == null) throw new ArgumentNullException("chars");
            if (chars.Length == 0) throw new ArgumentOutOfRangeException("chars");

            Value = chars[0];

            if (chars.Length > 1)
            {
                var overflow = chars.Skip(1).ToArray();
                if (overflow.Length > 0)
                {
                    Add(overflow);
                }
            }
            else
            {
                Eow = true;
            }
        }

        public IEnumerable<string> Similar(string word, int edits)
        {
            var words = new List<Word>();
            SimScan(word, new string(word.ToCharArray()), edits, 0, words);
            return words.OrderBy(w => w.Distance).Select(w => w.Value);
        }

        private void SimScan(string word, string state, int edits, int index, IList<Word> words)
        {
            var childIndex = index + 1;
            foreach (var child in GetChildren())
            {
                var tmp = index == state.Length ? state + child.Value : state.ReplaceAt(index, child.Value);
                if (Levenshtein.Distance(word, tmp) <= edits)
                {
                    if (child.Eow)
                    {
                        var potential = tmp.Substring(0, childIndex);
                        var distance = Levenshtein.Distance(word, potential);
                        if (distance <= edits)
                        {
                            words.Add(new Word { Value = potential, Distance = distance });
                        }
                    }
                    child.SimScan(word, tmp, edits, childIndex, words);
                }
            }
        }

        public bool HasWord(string word)
        {
            var nodes = new List<char>();
            Trie child;
            if (TryResolveChild(word[0], out child))
            {
                child.ExactScan(word, nodes);
            }
            if (nodes.Count > 0) return true;
            return false;
        }

        private void ExactScan(string word, List<char> chars)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            if (word.Length == 1 && word[0] == Value)
            {
                // The scan has reached its destination.
                if (Eow)
                {
                    chars.Add(Value);
                }
            }
            else if (word[0] == Value)
            {
                Trie child;
                if (TryResolveChild(word[1], out child))
                {
                    child.ExactScan(word.Substring(1), chars);
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
            return words.Select(s => new string(s.ToArray()));
        }

        private void PrefixScan(List<char> state, string prefix, List<List<char>> words)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == Value)
            {
                // The scan has reached its destination. Find words derived from this node.
                if (Eow) words.Add(state);
                foreach (var node in GetChildren())
                {
                    var newState = new List<char>(state.Count + 1);
                    foreach (var c in state) newState.Add(c);
                    newState.Add(node.Value);
                    node.PrefixScan(newState, new string(new[] { node.Value }), words);
                }
            }
            else if (prefix[0] == Value)
            {
                Trie child;
                if (TryResolveChild(prefix[1], out child))
                {
                    child.PrefixScan(state, prefix.Substring(1), words);
                }
            }
        }

        public void Add(string word)
        {
            Add(word.ToCharArray());
        }

        public void Add(char[] word)
        {
            if (word == null) throw new ArgumentNullException("word");
            if (word.Length == 0) throw new ArgumentOutOfRangeException("word");

            Trie child;
            if (!TryResolveChild(word[0], out child))
            {
                child = new Trie(word);
                Add(child);
            }
            else
            {
                child.Append(word);
            }
        }

        public void Add(Trie child)
        {
            try
            {
                _nodes.Add(child.Value, child);
            }
            catch (Exception ex)
            {
                
            }
        }

        public void Remove(Trie child)
        {
            _nodes.Remove(child.Value);
        }

        protected virtual bool TryResolveChild(char c, out Trie child)
        {
            return _nodes.TryGetValue(c, out child);
        }

        private void Append(IEnumerable<char> text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var list = text.ToArray();
            if (list[0] != Value) throw new ArgumentOutOfRangeException("text");

            var overflow = list.Skip(1).ToArray();
            if (overflow.Length > 0)
            {
                Add(overflow);
            }
            else
            {
                Eow = true;
            }
        }

        public void Remove(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("chars");

            Trie child;
            if (TryResolveChild(word[0], out child))
            {
                if (child.Count == 0)
                {
                    Remove(child);
                }
                else
                {
                    child.Eow = false;
                }
                if (word.Length > 1) child.Remove(word.Substring(1));
            }
        }

        public string ToString(IFormatProvider formatProvider)
        {
            return Value.ToString(formatProvider);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.CurrentCulture);
        }

        public virtual void Dispose()
        {
        }
    }
}