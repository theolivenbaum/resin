using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;

namespace Resin.IO
{
    public class DocumentFile : IDisposable
    {
        private readonly string _directory;
        private readonly string _containerId;
        private StreamWriter _writer;
        private readonly HashSet<string> _deletions;
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentFile));
        
        public string Id { get { return _containerId; } }

        public DocumentFile(string directory, string containerId)
        {
            _directory = directory;
            _containerId = containerId;
            _deletions = new HashSet<string>();
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                    File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.Read) :
                    File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream, Encoding.ASCII); // TODO: store token hashes instead of tokens? Tokens are Unicode.
                _writer.AutoFlush = true;
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
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

        public void Remove(string docId)
        {
            _deletions.Add(docId);
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();

                if (_deletions.Count == 0) return;

                var fileName = Path.Combine(_directory, _containerId + ".dc");
                var lines = File.ReadAllLines(fileName).ToList();
                using (var fs = File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read))
                using (var w = new StreamWriter(fs, Encoding.ASCII))
                {
                    foreach (var line in lines)
                    {
                        var docId = line.Substring(0, line.IndexOf(':'));
                        if (_deletions.Contains(docId))
                        {
                            _deletions.Remove(docId);
                        }
                        else
                        {
                            w.WriteLine(line);
                        }
                    }
                }
            }
        }

        //private IEnumerable<string> ReadAllLines(string fileName)
        //{
        //    using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        //    using (var sr = new StreamReader(fs, Encoding.ASCII))
        //    {
        //        string line;
        //        while ((line = sr.ReadLine()) != null)
        //        {
        //            yield return line;
        //        }
        //    }
        //} 
    }
}