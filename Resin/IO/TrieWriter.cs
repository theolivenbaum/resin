using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Resin.IO
{
    public class TrieWriter : IDisposable
    {
        private readonly string _fileId;

        public string Id { get { return _fileId; } }

        private StreamWriter _writer;

        public TrieWriter(string fileId, string directory)
        {
            _fileId = fileId;
            InitWriteSession(Path.Combine(directory, _fileId + ".tc"));
        }

        public void Write(Trie node)
        {
            _writer.Write(node.Value);
            _writer.Write(node.Eow ? "1" : "0");
            _writer.WriteLine(node.ChildCount);
            Print(node.Nodes.ToList());
        }

        private void Print(IList<Trie> nodes)
        {
            foreach (var node in nodes)
            {
                _writer.Write(node.Value);
                _writer.Write(node.Eow ? "1" : "0");
                _writer.WriteLine(node.ChildCount);
            }
            foreach (var node in nodes)
            {
                Print(node.Nodes.ToList());
            }
        }
        
        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                    File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read) :
                    File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream, Encoding.Unicode);
                //_writer.AutoFlush = false;
            }
        }

        public void Dispose()
        {
            _writer.Flush();
            _writer.Close();
            _writer.Dispose();
        }
    }
}