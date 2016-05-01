using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Resin.IO
{
    [Serializable]
    public class DocContainer : CompressedFileBase<DocContainer>, IDisposable
    {
        private readonly string _id;
        public string Id { get { return _id; } }

        /// <summary>
        /// docid/id (in file)
        /// </summary>
        private readonly Dictionary<string, string> _ids;

        [NonSerialized]
        private StreamWriter _writer;

        public DocContainer(string id, string directory)
        {
            _id = id;
            _ids = new Dictionary<string, string>();
            
            var fileName = Path.Combine(directory, _id + ".dc");

            Init(fileName);
        }

        private void Init(string fileName)
        {
            var fileStream = File.Exists(fileName) ?
                File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.Read) :
                File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

            _writer = new StreamWriter(fileStream);
            _writer.AutoFlush = false;
        }

        public Document Get(string docId, string directory)
        {
            var timer = new Stopwatch();
            timer.Start();
            var id = _ids[docId];
            var base64 = string.Empty;
            var fileName = Path.Combine(directory, _id + ".dc");
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                while (reader.Peek() >= 0) 
                {
                    var row = reader.ReadLine();
                    var endOfId = row.IndexOf(':');
                    var lineId = row.Substring(0, endOfId);
                    if(lineId != id) continue;
                    base64 = row.Substring(endOfId + 1);
                }
            }
            var rawBytes = Convert.FromBase64String(base64);
            var decompressed = QuickLZ.decompress(rawBytes);
            using (var memStream = new MemoryStream(decompressed))
            {
                var obj = (Document)Serializer.Deserialize(memStream);
                Log.DebugFormat("read {0} in {1}", fileName, timer.Elapsed);
                return obj;
            }
        }

        public void Put(Document doc, string directory)
        {
            var id = Path.GetRandomFileName();
            _ids[doc.Id] = id;
            var fileName = Path.Combine(directory, _id + ".dc");
            if(_writer == null) Init(fileName);
            using (var memStream = new MemoryStream())
            {
                Serializer.Serialize(memStream, doc);
                var bytes = memStream.ToArray();
                var compressed = QuickLZ.compress(bytes, 1);
                var base64 = Convert.ToBase64String(compressed);
                _writer.WriteLine("{0}:{1}", id, base64);
                _writer.Flush();
            }
        }

        public bool TryGet(string docId, string directory, out Document doc)
        {
            if (_ids.ContainsKey(docId))
            {
                doc = Get(docId, directory);
                return true;
            }
            doc = null;
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
                _writer.Close();
                _writer.Dispose();
            }
        }
    }
}