using System;
using System.Diagnostics;
using System.IO;

namespace Resin.IO
{
    public class DocumentReader : IDisposable
    {
        private readonly StreamReader _sr;

        public DocumentReader(StreamReader sr)
        {
            _sr = sr;
        }

        public Document Get(string docId)
        {
            var timer = new Stopwatch();
            timer.Start();

            string id = string.Empty;
            string line;
            var data = string.Empty;

            while ((line = _sr.ReadLine()) != null)
            {
                id = line.Substring(0, line.IndexOf(':'));
                if (id == docId)
                {
                    data = line;
                    break;
                }
            }

            var base64 = data.Substring(id.Length + 1);
            var bytes = Convert.FromBase64String(base64);

            using (var memStream = new MemoryStream(bytes))
            {
                return Deserialize(memStream);
            }
        }

        private Document Deserialize(Stream stream)
        {
            return (Document)FileBase.Serializer.Deserialize(stream);
        }

        public void Dispose()
        {
            if (_sr != null)
            {
                _sr.Dispose();
            }
        }
    }
}