using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Resin.IO
{
    public class TrieWriter : IDisposable
    {
        private readonly string _fileId;
        private readonly IFormatProvider _formatProvider;

        public string Id { get { return _fileId; } }

        private StreamWriter _writer;

        public TrieWriter(string fileId, string directory, IFormatProvider formatProvider)
        {
            _fileId = fileId;
            _formatProvider = formatProvider;
            var fileName = Path.Combine(directory, _fileId + ".tc");
            InitWriteSession(fileName);
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

        public void Write(Trie trie)
        {
            trie.Write(_writer, _formatProvider);
        }

        public void Dispose()
        {
            _writer.Flush();
            _writer.Close();
            _writer.Dispose();
        }
    }
}