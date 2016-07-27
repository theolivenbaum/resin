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

        public TrieReader(StreamReader reader)
        {
            _reader = reader;
        }

        //public IEnumerable<string> Similar(string word, int edits)
        //{

        //}

        public IEnumerable<string> Prefixed(string word)
        {
            SeekToBeginningOfFile();
            var node = FindNode(word);
            if (node != null)
            {
                if (node.Value.EoW) yield return word;

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
                GotoHeader(string.Format(":{0}{1}", level, c));              
            }
            while (true)
            {
                Step();
                if (_lastReadObject is Node)
                {
                    var node = (Node) _lastReadObject;
                    tip.Nodes.Add(node.Value, new Trie(node.Value, node.EoW));
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

        private void SeekToBeginningOfFile()
        {
            _reader.BaseStream.Position = 0;
            _reader.DiscardBufferedData();
            Step();
        }

        public bool HasWord(string word)
        {
            SeekToBeginningOfFile();
            var node = FindNode(word);
            if (node != null && node.Value.EoW) return true;
            return false;
        }

        private Node? FindNode(string word)
        {
            var lastIndex = word.Length - 1;
            for (int level = 0; level < word.Length; level++)
            {
                var c = word[level];
                var line = GotoNode(level, c);
                if (line == null) return null;
                var thisIsTheLastChar = level == lastIndex;
                if (thisIsTheLastChar)
                {
                    return line;
                }
                var header = string.Format(":{0}{1}", level + 1, word[level]);
                GotoHeader(header);
                if (!(IsHeader(_lastReadObject)))
                {
                    return null;
                }
            }
            return null;
        }

        private bool IsHeader(object obj)
        {
            return obj is string;
        }
        
        private void GotoHeader(string header)
        {
            while (true)
            {
                Step();
                if (IsHeader(_lastReadObject) && (string) _lastReadObject == header) break;
            }
        }

        private Node? GotoNode(int level, char value)
        {
            while (true)
            {
                Step();
                if (_lastReadObject == null) break;
                if (_lastReadObject is Node)
                {
                    var node = (Node) _lastReadObject;
                    if (node.IsMinValue() == false && node.Level == level && node.Value == value)
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
                return;
            }
            _lastReadObject = ParseNode(line);
        }

        private struct Node
        {
            public readonly int Level; 
            public readonly bool EoW;
            public readonly char Value;
            
            //public static Node MinValue()
            //{
            //    return new Node(isMinValue: true);
            //}

            //public Node(bool isMinValue)
            //{
            //    Level = -1;
            //    EoW = false;
            //    Value = (char)0;
            //}
            
            public Node(int level, char value, bool eow)
            {
                Level = level;
                EoW = eow;
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString(CultureInfo.CurrentCulture);
            }


            public bool IsMinValue()
            {
                return Level == -1 && EoW == false && Value == (char) 0;
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