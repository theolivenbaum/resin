using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using log4net;

namespace Resin.IO
{
    public class PostingsContainer : IDisposable
    {
        private readonly string _directory;
        private readonly string _containerId;
        private Dictionary<string, PostingsFile> _postingsFiles;
        private StreamWriter _writer;
        private static readonly ILog Log = LogManager.GetLogger(typeof(PostingsContainer));

        public string Id { get { return _containerId; } }

        public PostingsContainer(string directory, string containerId, bool eager = true)
        {
            _directory = directory;
            _containerId = containerId;

            var fileName = Path.Combine(_directory, _containerId + ".pc");
            if (eager && File.Exists(fileName))
            {
                Load(fileName);
            }
        }

        private void Load(string fileName)
        {
            InitReadSession();

            var timer = new Stopwatch();
            timer.Start();
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string readerToken = line.Substring(0, line.IndexOf(':'));
                    var base64 = line.Substring(readerToken.Length + 1);
                    var bytes = Convert.FromBase64String(base64);
                    using (var memStream = new MemoryStream(bytes))
                    {
                        var obj = Deserialize(memStream);
                        _postingsFiles[readerToken] = obj;
                    }
                }
            }
            Log.DebugFormat("read {0} in {1}", fileName, timer.Elapsed);
        }

        private void InitWriteSession(string fileName)
        {
            if (_writer == null)
            {
                var fileStream = File.Exists(fileName) ?
                 File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read) :
                 File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                _writer = new StreamWriter(fileStream);
                _writer.AutoFlush = true;
            }
        }

        private void InitReadSession()
        {
            if (_postingsFiles == null) _postingsFiles = new Dictionary<string, PostingsFile>();
        }

        public PostingsFile Get(string token)
        {
            InitReadSession();

            PostingsFile pf;
            if (_postingsFiles.TryGetValue(token, out pf))
            {
                return pf;
            }
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, _containerId + ".pc");
            using(var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                var data = string.Empty;
                var readerToken = string.Empty;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    readerToken = line.Substring(0, line.IndexOf(':'));
                    if (readerToken == token)
                    {
                        data = line;
                        break;
                    }
                }
                var base64 = data.Substring(readerToken.Length + 1);
                var bytes = Convert.FromBase64String(base64);
                using (var memStream = new MemoryStream(bytes))
                {
                    var obj = Deserialize(memStream);
                    _postingsFiles[token] = obj;
                    Log.DebugFormat("extracted {0} from {1} in {2}", obj, fileName, timer.Elapsed);
                    return obj;
                }   
            }
        }

        protected virtual PostingsFile Deserialize(Stream stream)
        {
            return (PostingsFile)FileBase.Serializer.Deserialize(stream);
        }

        private byte[] Serialize(PostingsFile item)
        {
            using (var stream = new MemoryStream())
            {
                FileBase.Serializer.Serialize(stream, item);
                return stream.ToArray();
            }
        }

        public void Put(PostingsFile item)
        {
            if (_postingsFiles == null) _postingsFiles = new Dictionary<string, PostingsFile>();

            _postingsFiles[item.Token] = item;
        }

        public bool TryGet(string token, out PostingsFile item)
        {
            InitReadSession();

            PostingsFile pf;
            if (_postingsFiles.TryGetValue(token, out pf))
            {
                item = pf;
                return true;
            }

            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, _containerId + ".pc");
            if (!File.Exists(fileName))
            {
                item = null;
                return false;
            }
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.ASCII))
            {
                var readerToken = string.Empty;
                string line;
                string data = null;
                while ((line = reader.ReadLine()) != null)
                {
                    readerToken = line.Substring(0, line.IndexOf(':'));
                    if (readerToken == token)
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
                var base64 = data.Substring(readerToken.Length + 1);
                var bytes = Convert.FromBase64String(base64);
                using (var memStream = new MemoryStream(bytes))
                {
                    var obj = Deserialize(memStream);
                    _postingsFiles[token] = obj;
                    Log.DebugFormat("extracted {0} from {1} in {2}", obj, fileName, timer.Elapsed);
                    item = obj;
                    return true;
                }
            }
        }

        public void Remove(string token)
        {
            _postingsFiles.Remove(token);
        }

        public int Count { get { return _postingsFiles.Count; } }

        public void Flush(string directory)
        {
            var fileName = Path.Combine(directory, _containerId + ".pc");
            InitWriteSession(fileName);
            foreach (var item in _postingsFiles.Values)
            {
                var bytes = Serialize(item);
                var base64 = Convert.ToBase64String(bytes);
                _writer.WriteLine("{0}:{1}", item.Token, base64);
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