using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    [ProtoContract]
    public class Trie
    {
        [ProtoMember(1)]
        private readonly char _value;

        [ProtoMember(2)]
        private bool _eow;

        [ProtoMember(3, DataFormat = DataFormat.Group)]
        private readonly IDictionary<char, Trie> _children;

        public Trie()
        {
            _children = new Dictionary<char, Trie>();
        }

        public Trie(IList<string> words) : this()
        {
            if (words == null) throw new ArgumentNullException("words");

            foreach (var word in words)
            {
                Add(word);
            }
        }

        private Trie(string text) : this()
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("word");

            _value = text[0];

            if (text.Length > 1)
            {
                var overflow = text.Substring(1);
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
        
        public IEnumerable<string> Similar(string word, int distance)
        {
            var words = new List<string>();
            SimScan(word, word, distance, 0, words);
            return words;
        }

        private void SimScan(string word, string state, int distance, int index, IList<string> words)
        {
            var childIndex = index + 1;
            foreach (var child in _children.Values)
            {
                var tmp = index == state.Length ? state + child._value : state.ReplaceAt(index, child._value);
                if (Levenshtein.Distance(word, tmp) <= distance)
                {
                    if (child._eow) words.Add(tmp);
                    child.SimScan(word, tmp, distance, childIndex, words);  
                }
            }
        }

        public IEnumerable<string> Prefixed(string prefix)
        {
            var words = new List<string>();
            Trie child;
            if (_children.TryGetValue(prefix[0], out child))
            {
                child.PrefixScan(prefix, prefix, words);
            }
            return words;
        }

        private void PrefixScan(string state, string prefix, List<string> words)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination. Find words derived from this node.
                if (_eow) words.Add(state);
                foreach (var node in _children.Values)
                {
                    node.PrefixScan(state+node._value, new string(new []{node._value}), words);
                }
            }
            else if (prefix[0] == _value)
            {
                Trie child;
                if (_children.TryGetValue(prefix[1], out child))
                {
                    child.PrefixScan(state, prefix.Substring(1), words);
                }
            }
        }

        public void Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            Trie child;
            if (!_children.TryGetValue(word[0], out child))
            {
                child = new Trie(word);
                _children.Add(word[0], child);
            }
            else
            {
                child.Append(word);
            }
        }

        private void Append(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("word");
            if (text[0] != _value) throw new ArgumentOutOfRangeException("text");

            var overflow = text.Substring(1);
            if (overflow.Length > 0)
            {
                Add(overflow);
            }
        }

        public void Save(string fileName)
        {
            using (var fs = File.Create(fileName))
            {
                Serializer.Serialize(fs, this);
            }
        }

        public static Trie Load(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                return Serializer.Deserialize<Trie>(file);
            }
        }

        public void Remove(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("word");

            Trie child;
            if (_children.TryGetValue(word[0], out child))
            {
                if (child._children.Count == 0)
                {
                    _children.Remove(child._value);
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