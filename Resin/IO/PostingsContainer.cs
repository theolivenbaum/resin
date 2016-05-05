using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Resin.IO
{
    [Serializable]
    public class PostingsContainer : CompressedFileBase<PostingsContainer>, IDisposable
    {
        private readonly Dictionary<string, string> _ids;
        private const string ItemsFileExtIncDot = ".pc";
        private readonly string _containerId;
        
        public string Id { get { return _containerId; } }

        [NonSerialized] 
        private Dictionary<string, PostingsFile> _postingsFiles;
        [NonSerialized]
        private StreamWriter _writer;

        public PostingsContainer(string containerId)
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

                _writer = new StreamWriter(fileStream);
                _writer.AutoFlush = false;
            }
        }

        private void InitReadSession()
        {
            if (_postingsFiles == null) _postingsFiles = new Dictionary<string, PostingsFile>();
        }

        public PostingsFile Get(string itemId, string directory)
        {
            InitReadSession();

            PostingsFile pf;
            if (_postingsFiles.TryGetValue(itemId, out pf))
            {
                return pf;
            }
            var timer = new Stopwatch();
            timer.Start();
            var id = _ids[itemId];
            var fileName = Path.Combine(directory, _containerId + ItemsFileExtIncDot);
            using(var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
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
                    var obj = Deserialize(memStream);
                    _postingsFiles[itemId] = obj;
                    Log.DebugFormat("extracted {0} from {1} in {2}", obj, fileName, timer.Elapsed);
                    return obj;
                }   
            }
        }

        protected virtual PostingsFile Deserialize(Stream stream)
        {
            return (PostingsFile)Serializer.Deserialize(stream);
        }

        //protected virtual void Serialize(Stream stream, TItem item)
        //{
        //    Serializer.Serialize(stream, item);
        //}

        private byte[] Serialize(PostingsFile item)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, item);
                return stream.ToArray();
            }
        }

        public void Put(PostingsFile item)
        {
            if (_postingsFiles == null) _postingsFiles = new Dictionary<string, PostingsFile>();

            _postingsFiles[item.Id] = item;
            var id = Path.GetRandomFileName();
            _ids[item.Id] = id;
        }

        public bool TryGet(string itemId, string directory, out PostingsFile item)
        {
            if (_ids.ContainsKey(itemId))
            {
                item = Get(itemId, directory);
                return true;
            }
            item = null;
            return false;
        }

        public void Remove(string itemId)
        {
            _ids.Remove(itemId);
        }

        public int Count { get { return _ids.Count; } }

        public void Flush(string directory)
        {
            var fileName = Path.Combine(directory, _containerId + ItemsFileExtIncDot);
            InitWriteSession(fileName);
            foreach (var item in _postingsFiles.Values)
            {
                var bytes = Serialize(item);
                var base64 = Convert.ToBase64String(bytes);
                _writer.WriteLine("{0}:{1}", _ids[item.Id], base64);
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