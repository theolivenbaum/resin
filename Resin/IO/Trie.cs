using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO
{
    public class Trie
    {
        public char Value { get; protected set; }

        public bool Eow { get; protected set; }

        public Dictionary<char, Trie> Nodes { get; set; }

        public Trie()
        {
            Nodes = new Dictionary<char, Trie>();
        }

        public Trie(char value, bool eow) : this()
        {
            Value = value;
            Eow = eow;
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

        public void Write(StreamWriter writer, IFormatProvider formatProvider, int level = 0)
        {
            var sorted = Nodes.Values.OrderBy(s => s.Value).ToList();
            var nextLevel = level + 1;
            var header = string.Format(formatProvider, ":{0}{1}", level, Value == char.MinValue ? ' ' : Value);
            writer.WriteLine(header);
            foreach (var node in sorted)
            {
                writer.WriteLine(Format, level, node.ToString(formatProvider));
            }
            foreach (var node in sorted)
            {
                node.Write(writer, formatProvider, nextLevel);
            }
        }

        protected virtual bool TryResolveChild(char c, out Trie trie)
        {
            return Nodes.TryGetValue(c, out trie);
        }

        protected virtual ICollection<Trie> ResolveChildren()
        {
            return Nodes.Values;
        }

        public IEnumerable<string> Similar(string word, int edits)
        {
            var words = new List<Word>();
            SimScan(word, word, edits, 0, words);
            return words.OrderBy(w => w.Distance).Select(w => w.Value);
        }

        private struct Word
        {
            public string Value;
            public int Distance;
        }

        private void SimScan(string word, string state, int edits, int index, IList<Word> words)
        {
            var childIndex = index + 1;
            foreach (var child in ResolveChildren())
            {
                var tmp = index == state.Length ? state + child.Value : state.ReplaceAt(index, child.Value);
                var distance = Levenshtein.Distance(word, tmp);
                if (distance <= edits)
                {
                    if (child.Eow)
                    {
                        words.Add(new Word { Value = tmp, Distance = distance });
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
                if (Nodes.TryGetValue(word[1], out child))
                {
                    child.ExactScan(word.Substring(1), chars);
                }
            }
        }

        public Trie FindNode(string word)
        {
            Trie child;
            if (TryResolveChild(word[0], out child))
            {
                return child.FindNodeScan(word);
            }
            return null;
        }

        public Trie FindNodeScan(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            if (word.Length == 1 && word[0] == Value)
            {
                // The scan has reached its destination.
                return this;
            }

            if(word[0] != Value) return null;

            foreach (var node in Nodes.Values)
            {
                return node.FindNodeScan(word.Substring(1));
            }
            return null;
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
                foreach (var node in Nodes.Values)
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
                if (Nodes.TryGetValue(prefix[1], out child))
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
            if (!Nodes.TryGetValue(word[0], out child))
            {
                child = new Trie(word);
                Nodes.Add(word[0], child);
            }
            else
            {
                child.Append(word);
            }
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
            if (Nodes.TryGetValue(word[0], out child))
            {
                if (child.Nodes.Count == 0)
                {
                    Nodes.Remove(child.Value);
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
            return string.Format(formatProvider, Format,
                Value, 
                Eow ? "1" : "0");
        }

        private const string Format = "{0}\t{1}";
    }
}