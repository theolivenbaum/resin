using System;
using System.IO;
using System.Text;

namespace Resin.IO
{
    public class TrieWriter : IDisposable
    {
        private readonly string _containerId;

        public string Id { get { return _containerId; } }

        private StreamWriter _writer;

        public TrieWriter(string containerId)
        {
            _containerId = containerId;
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                    File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read) :
                    File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream, Encoding.Unicode);
                _writer.AutoFlush = false;
            }
        }

        public void Put(Trie trie, string directory)
        {
            var id = string.Format("{0}.{1}", trie.Val, trie.Depth);
            var fileName = Path.Combine(directory, _containerId + ".tc");
            InitWriteSession(fileName);
            _writer.WriteLine("{0}:{1}", id, trie.Eow ? 1 : 0);
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
            }
        }
    }

    public class TrieReader : IDisposable
    {
        private readonly StreamReader _reader;

        public TrieReader(string containerId, string directory)
        {
            var fileName = Path.Combine(directory, containerId + ".tc");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new StreamReader(fs, Encoding.Unicode);
        }

        public bool TryStep(out Trie node)
        {
            var line = _reader.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                node = null;
                return false;
            }
            var id = line.Substring(0, line.IndexOf(':'));
            var val = line[0];
            var eow = int.Parse(line.Substring(id.Length + 1)) == 1;
            var depth = Int32.Parse(id.Substring(id.IndexOf('.') + 1));
            node = new Trie(val, depth, eow);
            return true;
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