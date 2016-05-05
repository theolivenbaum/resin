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
                _writer.AutoFlush = true;
            }
        }

        public void Put(LazyTrie trie, string directory)
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
}