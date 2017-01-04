using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using log4net;

namespace Resin.IO
{
    public class DocumentFile : IDisposable
    {
        private readonly string _directory;
        private readonly string _containerId;
        private volatile StreamWriter _writer;
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentFile));
        private static readonly object Sync = new object();
 
        public string Id { get { return _containerId; } }

        public DocumentFile(string directory, string containerId)
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

        public Document Get(string docId)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, _containerId + ".dc");
            using(var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                string readerId = string.Empty;
                string line;
                var data = string.Empty;
                while ((line = reader.ReadLine()) != null)
                {
                    readerId = line.Substring(0, line.IndexOf(':'));
                    if (readerId == docId)
                    {
                        data = line;
                        break;
                    }
                }
                var base64 = data.Substring(readerId.Length + 1);
                var bytes = Convert.FromBase64String(base64);
                using (var memStream = new MemoryStream(bytes))
                {
                    var doc = Deserialize(memStream);
                    Log.DebugFormat("extracted {0} from {1} in {2}", doc.Id, fileName, timer.Elapsed);
                    return doc;
                }
            }
        }

        private Document Deserialize(Stream stream)
        {
            return (Document)FileBase.Serializer.Deserialize(stream);
        }

        private byte[] Serialize(Document item)
        {
            using (var stream = new MemoryStream())
            {
                FileBase.Serializer.Serialize(stream, item);
                return stream.ToArray();
            }
        }

        public void Put(Document item, string directory)
        {
            var fileName = Path.Combine(directory, _containerId + ".dc");
            InitWriteSession(fileName);
            var bytes = Serialize(item);
            if(bytes.Length == 0) throw new Exception();
            var base64 = Convert.ToBase64String(bytes);
            _writer.WriteLine("{0}:{1}", item.Id, base64);
        }

        public bool TryGet(string docId, out Document item)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, _containerId + ".dc");
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                string readerId = string.Empty;
                string line;
                string data = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        item = null;
                        return false;
                    }
                    readerId = line.Substring(0, line.IndexOf(':'));
                    if (readerId == docId)
                    {
                        data = line;
                        break;
                    }
                }
                if (data == null)
                {
                    item = null;
                    return false;
                }
                var base64 = data.Substring(readerId.Length + 1);
                var bytes = Convert.FromBase64String(base64);
                using (var memStream = new MemoryStream(bytes))
                {
                    var doc = Deserialize(memStream);
                    Log.DebugFormat("extracted {0} from {1} in {2}", doc.Id, fileName, timer.Elapsed);
                    item = doc;
                    return true;
                }
            }
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