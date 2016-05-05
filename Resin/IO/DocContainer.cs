using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Resin.IO
{
    [Serializable]
    public class DocContainer : CompressedFileBase<DocContainer>, IDisposable
    {
        /// <summary>
        /// itemid/id (in file)
        /// </summary>
        private readonly Dictionary<string, string> _ids;
        private readonly string _containerId;

        public string Id { get { return _containerId; } }

        [NonSerialized]
        private StreamWriter _writer;

        public DocContainer(string containerId)
        {
            _containerId = containerId;
            _ids = new Dictionary<string, string>();
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                    File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.Read) :
                    File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream, Encoding.ASCII);
                _writer.AutoFlush = true;
            }
        }

        public Document Get(string docId, string directory)
        {
            var timer = new Stopwatch();
            timer.Start();
            var id = _ids[docId];
            var fileName = Path.Combine(directory, _containerId + ".dc");
            using(var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                reader.BaseStream.Position = 0;
                reader.DiscardBufferedData();
                var data = string.Empty;
                string lineId = string.Empty;
                while (reader.Peek() >= 0)
                {
                    var row = reader.ReadLine();
                    var indexOfDelimiter = row.IndexOf(':');
                    lineId = row.Substring(0, indexOfDelimiter);
                    if (lineId == id)
                    {
                        data = row;
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(data))
                {
                    throw new Exception();
                }
                var base64 = data.Substring(lineId.Length + 1);
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
            return (Document)Serializer.Deserialize(stream);
        }

        private byte[] Serialize(Document item)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, item);
                return stream.ToArray();
            }
        }

        public void Put(Document item, string directory)
        {
            var id = Path.GetRandomFileName();
            _ids[item.Id] = id;
            var fileName = Path.Combine(directory, _containerId + ".dc");
            InitWriteSession(fileName);
            var bytes = Serialize(item);
            var base64 = Convert.ToBase64String(bytes);
            _writer.WriteLine("{0}:{1}", id, base64);
        }

        public bool TryGet(string docId, string directory, out Document item)
        {
            if (_ids.ContainsKey(docId))
            {
                item = Get(docId, directory);
                return true;
            }
            item = null;
            return false;
        }

        public void Remove(string docId)
        {
            _ids.Remove(docId);
        }

        public int Count { get { return _ids.Count; } }
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