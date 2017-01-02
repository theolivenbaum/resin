using System;
using System.IO;
using System.Linq;

namespace Resin.IO
{
    public class TrieStreamReader : IDisposable
    {
        protected readonly StreamReader StreamReader;

        public TrieStreamReader(StreamReader streamReader)
        {
            StreamReader = streamReader;
        }

        public void ResolveChildren(Trie node)
        {
            for (int i = 0; i < node.Count; i++)
            {
                node.Add(Read());
            }
            foreach (var child in node.Nodes)
            {
                ResolveChildren(child);
            }
        }

        public Trie Read()
        {
            var line = StreamReader.ReadLine();
            if (line == null) return null;
            return ParseNode(line);
        }

        private Trie ParseNode(string line)
        {
            var val = line[0];
            var eow = line[1] == '1';
            var count = int.Parse(line.Substring(2, line.Length-2));
            return new TrieScanner(val, eow, count, this);
        }

        public void Dispose()
        {
            if (StreamReader != null)
            {
                StreamReader.Close();
                StreamReader.Dispose();
            }
        }
    }
}