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
        public char Value { get; set; }

        [ProtoMember(2)]
        public bool Eow { get; set; }

        [ProtoMember(3, DataFormat = DataFormat.Group)]
        public IDictionary<char, Trie> Children { get; set; }

        public Trie()
        {
            Children = new Dictionary<char, Trie>();
        }

        public Trie(IList<string> words)
        {
            if (words == null) throw new ArgumentNullException("words");

            Children = new Dictionary<char, Trie>();

            foreach (var word in words)
            {
                Trie child;
                if (!Children.TryGetValue(word[0], out child))
                {
                    child = new Trie(word, this);
                    Children.Add(word[0], child);
                }
                else
                {
                    child.Append(word);
                }
            }
        }

        public Trie(string text, Trie parent)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("text");
            }

            Value = text[0];

            Children = new Dictionary<char, Trie>();

            if (text.Length > 1)
            {
                var overflow = text.Substring(1);
                if (overflow.Length > 0)
                {
                    Trie child;
                    if (!Children.TryGetValue(overflow[0], out child))
                    {
                        child = new Trie(overflow, this);
                        Children.Add(overflow[0], child);
                    }
                    else
                    {
                        child.Append(overflow);
                    }
                }
            }
            else
            {
                Eow = true;
            }
        }

        public IEnumerable<string> GetTokens(string prefix)
        {
            var words = new List<string>();
            Trie child;
            if (Children.TryGetValue(prefix[0], out child))
            {
                child.Scan(prefix, prefix, ref words);
            }
            return words;
        }

        private void Scan(string originalPrefix, string prefix, ref List<string> words)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == Value)
            {
                if (Eow) words.Add(originalPrefix);
                foreach (var node in Children.Values)
                {
                    node.Scan(originalPrefix+node.Value, new string(new []{node.Value}), ref words);
                }
            }
            else if (prefix[0] == Value)
            {
                Trie child;
                if (Children.TryGetValue(prefix[1], out child))
                {
                    child.Scan(originalPrefix, prefix.Substring(1), ref words);
                }
            }
        }

        public void AppendToDescendants(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("text");
            Trie child;
            if (!Children.TryGetValue(text[0], out child))
            {
                child = new Trie(text, this);
                Children.Add(text[0], child);
            }
            else
            {
                child.Append(text);
            }
        }

        public void Append(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("text");
            if (text[0] != Value) throw new ArgumentOutOfRangeException("text");

            var overflow = text.Substring(1);
            if (overflow.Length > 0)
            {
                Trie child;
                if (!Children.TryGetValue(overflow[0], out child))
                {
                    child = new Trie(overflow, this);
                    Children.Add(overflow[0], child);
                }
                else
                {
                    child.Append(overflow);
                }
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
    }
}