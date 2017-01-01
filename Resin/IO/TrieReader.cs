using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO
{
    public class TrieReader : IDisposable
    {
        private readonly StreamReader _reader;

        public TrieReader(StreamReader reader)
        {
            _reader = reader;
        }

        public Trie ReadWholeTree()
        {
            var count = int.Parse(_reader.ReadLine() ?? "0");
            var root = new Trie();
            Populate(root, count);
            return root;
        }

        private void Populate(Trie trie, int count)
        {
            foreach (var node in ReadLines(count).ToList())
            {
                var subTree = new Trie(node.Value, node.Eow);
                trie.Nodes.Add(node.Value, subTree);
                Populate(subTree, node.Count);
            }
        }

        private IEnumerable<Node> ReadLines(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return ParseNode(_reader.ReadLine());
            }
        }

        private Node ParseNode(string line)
        {
            var val = line[0];
            var eow = line[1] == '1';
            var count = int.Parse(line.Substring(2, line.Length-2));
            return new Node(val, eow, count);
        }

        public struct Node
        {
            public readonly bool Eow;
            public readonly char Value;
            public readonly int Count;
                       
            public Node(char value, bool eow, int count)
            {
                Eow = eow;
                Value = value;
                Count = count;
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
}