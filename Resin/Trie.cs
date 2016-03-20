using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class Trie
    {
        public IDictionary<char, Trie> Children { get; set; }
        public Trie Parent { get; set; }
        public char Value { get; set; }
        public bool Eow { get; set; }

        public IEnumerable<string> WordsStartingWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (Parent == null)
            {
                Trie child;
                if (Children.TryGetValue(prefix[0], out child))
                {
                    return child.WordsStartingWith(prefix);
                }  
            }
            else if (prefix.Length == 1 && prefix[0] == Value)
            {
                return Descendants().Where(t => t.Eow).Select(t => t.Path());
            }
            else if (prefix[0] == Value)
            {
                Trie child;
                if (prefix.Length > 1 && Children.TryGetValue(prefix[1], out child))
                {
                    return child.WordsStartingWith(prefix.Substring(1));
                }
            }
            return Enumerable.Empty<string>();
        } 

        public IEnumerable<Trie> Descendants()
        {
            return Children.Values.SelectMany(c=>new []{c}.Concat(c.Descendants()));
        }

        public Trie(IEnumerable<string> words)
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
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("text");

            Value = text[0];
            Children = new Dictionary<char, Trie>();
            Parent = parent;

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

        public string Path()
        {
            return new string(PathAsChars().ToArray());
        }

        public IEnumerable<char> PathAsChars()
        {
            var path = new List<Trie>();
            var cursor = this;
            while (cursor != null)
            {
                path.Add(cursor);
                cursor = cursor.Parent;
            }
            return path.Select(n => n.Value).Reverse();
        }
    }
}