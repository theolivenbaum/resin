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

        [NonSerialized]
        private Dictionary<string, StreamReader> _readers; 

        /// <summary>
        /// docid/id (in file)
        /// </summary>
        private readonly Dictionary<string, string> _ids;

        [NonSerialized]
        private StreamWriter _writer;

        public DocContainer(string id)
        {
            _id = id;
            _ids = new Dictionary<string, string>();
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                 File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.Read) :
                 File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream);
                _writer.AutoFlush = false; 
            }
        }

        private void InitReadSession()
        {
            if (_readers == null) _readers = new Dictionary<string, StreamReader>();
        }

        public Document Get(string docId, string directory)
        {
            var timer = new Stopwatch();
            timer.Start();
            var id = _ids[docId];
            var fileName = Path.Combine(directory, _id + ".dc");
            InitReadSession();
            StreamReader reader;
            if (!_readers.TryGetValue(fileName, out reader))
            {
                var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                reader = new StreamReader(fs);
                _readers[fileName] = reader;
                Log.DebugFormat("opened {0}", fileName);
            }
            reader.BaseStream.Position = 0;
            reader.DiscardBufferedData();
            var data = string.Empty;
            string lineId;
            while (reader.Peek() >= 0)
            {
                var row = reader.ReadLine();
                lineId = row.Substring(0, 12);
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
            var base64 = data.Substring(13);
            var bytes = Convert.FromBase64String(base64);
            using (var memStream = new MemoryStream(bytes))
            {
                var obj = (Document)Serializer.Deserialize(memStream);
                Log.DebugFormat("read from {0} in {1}", fileName, timer.Elapsed);
                return obj;
            }
        }

        public void Put(Document doc, string directory)
        {
            var id = Path.GetRandomFileName();
            _ids[doc.Id] = id;
            var fileName = Path.Combine(directory, _id + ".dc");
            InitWriteSession(fileName);
            using (var memStream = new MemoryStream())
            {
                Serializer.Serialize(memStream, doc);
                var bytes = memStream.ToArray();
                var base64 = Convert.ToBase64String(bytes);
                _writer.WriteLine("{0}:{1}", id, base64);
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
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
            }
            if (_readers != null)
            {
                foreach (var reader in _readers.Values)
                {
                    reader.Dispose();
                }  
            }
        }
    }
}