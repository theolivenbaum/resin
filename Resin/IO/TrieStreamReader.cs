using System;
using System.IO;
using System.Text;

namespace Resin.IO
{
    public class TrieStreamReader : IDisposable
    {
        protected StreamReader StreamReader;
        private readonly FileStream _fileStream;

        public TrieStreamReader(FileStream fileStream)
        {
            _fileStream = fileStream;
           
        }

        public TrieScanner Reset()
        {
            TrieScanner.Skip = 0;
            _fileStream.Position = 0;
            StreamReader = new StreamReader(_fileStream, Encoding.Unicode);
            return (TrieScanner)Read();
        }

        public void Skip(int count)
        {
            while (true)
            {
                if (count == 0) return;
                var node = Read();
                count--;
                count += node.ChildCount;
            }
        }

        public void ResolveChildren(Trie node)
        {
            var children = node.ChildCount;
            for (int i = 0; i < children; i++)
            {
                var child = Read();
                child.Index = i;
                node.Add(child);
            }
        }

        private Trie Read()
        {
            var line = StreamReader.ReadLine();
            LastRead = line;
            if (line == null) return null;
            return ParseNode(line);
        }

        public string LastRead { get; private set; }

        private Trie ParseNode(string line)
        {
            var segs = line.Split(' ');
            var val = (char)int.Parse(segs[0]);
            var eow = segs[1] == "1";
            var count = int.Parse(segs[2]);
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