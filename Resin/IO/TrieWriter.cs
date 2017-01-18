using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Resin.IO
{
    public class TrieWriter : IDisposable
    {
        private readonly string _fileName;

        private StreamWriter _writer;

        public TrieWriter(string fileName)
        {
            _fileName = fileName;
            InitWriteSession();
        }

        public void Write(Trie node)
        {
            Print(node);
            Print(node.Nodes.ToList());
        }

        private void Print(IList<Trie> nodes)
        {
            foreach (var node in nodes)
            {
                Print(node);
            }
            foreach (var node in nodes)
            {
                Print(node.Nodes.ToList());
            }
        }

        private void Print(Trie node)
        {
            _writer.Write(((int)node.Value).ToString(CultureInfo.CurrentCulture));
            _writer.Write(' ');
            _writer.Write(node.Eow ? "1" : "0");
            _writer.Write(' ');
            _writer.WriteLine(node.ChildCount);
        }
        
        private void InitWriteSession()
        {
            if (_writer == null)
            {
                var fileStream = File.Open(_fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);

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