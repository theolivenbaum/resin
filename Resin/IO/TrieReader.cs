using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Resin.IO
{
    public class TrieReader : IDisposable
    {
        private readonly StreamReader _reader;
        private object _lastReadObject;
        private string _lastReadHeader;

        public TrieReader(StreamReader reader)
        {
            _reader = reader;
        }

        public bool HasWord(string word)
        {
            ResetCursor();
            var node = FindNode(word);
            if (node != null && node.Value.Eow) return true;
            return false;
        }

        public IEnumerable<string> Similar(string word, int edits)
        {
            ResetCursor();
            var words = new List<Trie.Word>();
            var state = new string(word.ToCharArray());
            SimScan(word, state, edits, 0, words);
            return words.OrderBy(w=>w.Distance).Select(w => w.Value);
        }

        private void SimScan(string word, string state, int edits, int index, IList<Trie.Word> words)
        {
            var childIndex = index + 1;
            var children = ResolveChildrenAt(index).ToList();

            foreach (var child in children)
            {
                var header = string.Format(":{0}{1}", childIndex, child.Value);
                if (header != _lastReadHeader) StepUntilHeader(header);

                var test = index == state.Length ? state + child.Value : state.ReplaceAt(index, child.Value);
                var distance = Levenshtein.Distance(word, test);
                if (distance <= edits)
                {
                    if (child.Eow)
                    {
                        words.Add(new Trie.Word {Value = test, Distance = distance});
                    }
                    SimScan(word, test, edits, childIndex, words);
                }
            }
        }

        private IEnumerable<Trie> ResolveChildrenAt(int level)
        {
            while (true)
            {
                Step();
                if (_lastReadObject == null)
                {
                    break;
                }
                if (_lastReadObject is Node)
                {
                    var node = (Node) _lastReadObject;
                    yield return new Trie(node.Value, node.Eow);
                }
                else
                {
                    if (level == 0) break;
                    var cursorLevel = int.Parse(((string) _lastReadObject).Substring(1, 1));
                    while (cursorLevel != level)
                    {
                        yield break;
                    }
                }
            }
        } 

        public IEnumerable<string> Prefixed(string word)
        {
            ResetCursor();
            var node = FindNode(word);
            if (node != null)
            {
                if (node.Value.Eow) yield return word;

                var nodes = new Queue<Trie>(word.Select(c => new Trie(c, false)));
                
                if (nodes.Count == 0) yield break;

                var root = new Trie();
                var parent = nodes.Dequeue();
                root.Nodes.Add(parent.Value, parent);
                while (nodes.Count > 0)
                {
                    var n = nodes.Dequeue();
                    parent.Nodes.Add(n.Value, n);
                    parent = n;
                }
                var tip = root.FindNode(word);
                ResolveWords(node.Value.Value, node.Value.Level + 1, tip);
                var result = root.Prefixed(word).ToList();
                foreach (var w in result)
                {
                    yield return w;
                }
            }
        }

        private void ResolveWords(char c, int level, Trie tip)
        {
            if (!IsHeader(_lastReadObject))
            {
                StepUntilHeader(string.Format(":{0}{1}", level, c));              
            }
            while (true)
            {
                Step();
                if (_lastReadObject is Node)
                {
                    var node = (Node) _lastReadObject;
                    tip.Nodes.Add(node.Value, new Trie(node.Value, node.Eow));
                }
                else
                {
                    break;
                }
            }
            var nextLevel = level + 1;
            foreach (var node in tip.Nodes.Values)
            {
                ResolveWords(node.Value, nextLevel, node);
            }
        }

        private void ResetCursor()
        {
            _reader.BaseStream.Position = 0;
            _reader.DiscardBufferedData();
            Step();
        }

        private Node? FindNode(string word)
        {
            var lastIndex = word.Length - 1;
            for (int level = 0; level < word.Length; level++)
            {
                var c = word[level];
                var line = StepUntilNode(level, c);
                if (line == null) return null;
                var thisIsTheLastChar = level == lastIndex;
                if (thisIsTheLastChar)
                {
                    return line;
                }
                var header = string.Format(":{0}{1}", level + 1, word[level]);
                StepUntilHeader(header);
                if (!(IsHeader(_lastReadObject)))
                {
                    return null;
                }
            }
            return null;
        }

        private bool IsHeader(object obj)
        {
            return obj == null || obj is string;
        }

        private void Step()
        {
            var line = _reader.ReadLine();
            if (line == null)
            {
                _lastReadObject = null;
                return;
            }
            if (line.StartsWith(":"))
            {
                _lastReadObject = line;
                _lastReadHeader = line;
                return;
            }
            _lastReadObject = ParseNode(line);
        }
        
        private void StepUntilHeader(string header)
        {
            while (true)
            {
                Step();
                if (IsHeader(_lastReadObject) && (string) _lastReadObject == header) break;
            }
        }

        private Node? StepUntilNode(int level, char value)
        {
            while (true)
            {
                Step();
                if (_lastReadObject == null) break;
                if (_lastReadObject is Node)
                {
                    var node = (Node) _lastReadObject;
                    if (node.Level == level && node.Value == value)
                    {
                        return node;
                    }
                }
            }
            return null;
        }

        private Node ParseNode(string line)
        {
            var segs = line.Split(new[] { "\t" }, StringSplitOptions.None);
            var level = int.Parse(segs[0], CultureInfo.CurrentCulture);
            var val = Char.Parse(segs[1]);
            var eow = segs[2] == "1";
            return new Node(level, val, eow);
        }

        private struct Node
        {
            public readonly int Level; 
            public readonly bool Eow;
            public readonly char Value;
                       
            public Node(int level, char value, bool eow)
            {
                Level = level;
                Eow = eow;
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString(CultureInfo.CurrentCulture);
            }
        }


        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
            }
        }
    }

    public interface ITrieReader
    {
        IEnumerable<string> Similar(string word, int edits);
        IEnumerable<string> Prefixed(string prefix);
        bool HasWord(string word);
    }
}