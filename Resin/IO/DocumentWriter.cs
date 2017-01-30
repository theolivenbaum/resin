using System;
using System.IO;
using System.Text;

namespace Resin.IO
{
    public class DocumentWriter : IDisposable
    {
        private readonly string _directory;
        private readonly string _containerId;
        private volatile StreamWriter _writer;
        private static readonly object Sync = new object();
 
        public string Id { get { return _containerId; } }

        public DocumentWriter(string directory, string containerId)
        {
            _directory = directory;
            _containerId = containerId;
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                lock (Sync)
                {
                    if (_writer == null)
                    {
                        var fileStream = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        _writer = new StreamWriter(fileStream, Encoding.ASCII);
                    }
                }
            }
        }

        private byte[] Serialize(Document item)
        {
            using (var stream = new MemoryStream())
            {
                FileBase.Serializer.Serialize(stream, item);
                return stream.ToArray();
            }
        }

        public void Write(Document item)
        {
            var fileName = Path.Combine(_directory, _containerId + ".dc");
            InitWriteSession(fileName);
            var bytes = Serialize(item);
            if(bytes.Length == 0) throw new Exception();
            var base64 = Convert.ToBase64String(bytes);
            _writer.WriteLine("{0}:{1}", item.Id, base64);
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