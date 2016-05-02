using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Resin.IO
{
    [Serializable]
    public class DocContainer : Container<DocContainer, Document>
    {
        public DocContainer(string containerId, string itemsFileExtIncDot) : base(containerId, itemsFileExtIncDot)
        {
        }
    }

    [Serializable]
    public class Container<TContainer, TItem> : CompressedFileBase<TContainer>, IDisposable where TItem : IDentifyable
    {
        /// <summary>
        /// itemid/id (in file)
        /// </summary>
        private readonly Dictionary<string, string> _ids;
        private readonly string _itemsFileExtIncDot;
        private readonly string _containerId;

        public string Id { get { return _containerId; } }

        [NonSerialized]
        private Dictionary<string, StreamReader> _readers;       

        [NonSerialized]
        private StreamWriter _writer;

        public Container(string containerId, string itemsFileExtIncDot)
        {
            _containerId = containerId;
            _ids = new Dictionary<string, string>();
            _itemsFileExtIncDot = itemsFileExtIncDot;
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

        public TItem Get(string docId, string directory)
        {
            var timer = new Stopwatch();
            timer.Start();
            var id = _ids[docId];
            var fileName = Path.Combine(directory, _containerId + _itemsFileExtIncDot);
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
                var obj = Deserialize(memStream);
                Log.DebugFormat("read from {0} in {1}", fileName, timer.Elapsed);
                return obj;
            }
        }

        protected virtual TItem Deserialize(Stream stream)
        {
            return (TItem)Serializer.Deserialize(stream);
        }

        protected virtual void Serialize(Stream stream, TItem item)
        {
            Serializer.Serialize(stream, item);
        }

        public void Put(TItem item, string directory)
        {
            var id = Path.GetRandomFileName();
            _ids[item.Id] = id;
            var fileName = Path.Combine(directory, _containerId + _itemsFileExtIncDot);
            InitWriteSession(fileName);
            using (var memStream = new MemoryStream())
            {
                Serialize(memStream, item);
                var bytes = memStream.ToArray();
                var base64 = Convert.ToBase64String(bytes);
                _writer.WriteLine("{0}:{1}", id, base64);
            }
        }

        public bool TryGet(string itemId, string directory, out TItem item)
        {
            if (_ids.ContainsKey(itemId))
            {
                item = Get(itemId, directory);
                return true;
            }
            item = default(TItem);
            return false;
        }

        public void Remove(string itemId)
        {
            _ids.Remove(itemId);
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