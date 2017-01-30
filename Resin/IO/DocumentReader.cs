using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using log4net;

namespace Resin.IO
{
    public class DocumentReader
    {
        private readonly string _directory;
        private readonly string _containerId;
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentWriter));
 
        public string Id { get { return _containerId; } }

        public DocumentReader(string directory, string containerId)
        {
            _directory = directory;
            _containerId = containerId;
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
                    Log.DebugFormat("read {0} from {1} in {2}", doc.Id, fileName, timer.Elapsed);
                    return doc;
                }
            }
        }

        private Document Deserialize(Stream stream)
        {
            return (Document)FileBase.Serializer.Deserialize(stream);
        }
    }
}