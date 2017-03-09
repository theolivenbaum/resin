using System;
using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : IDisposable
    {
        private readonly StreamReader _sr;

        public DocumentReader(StreamReader sr)
        {
            _sr = sr;
        }

        public Document Get(int docId)
        {
            string line;
            var data = string.Empty;

            while ((line = _sr.ReadLine()) != null)
            {
                var parts = line.Split(':');
                var test = int.Parse(parts[0]);

                if (test == docId)
                {
                    data = parts[1];
                    break;
                }
            }

            var bytes = Convert.FromBase64String(data);

            using (var memStream = new MemoryStream(bytes))
            {
                return Deserialize(memStream);
            }
        }

        private Document Deserialize(Stream stream)
        {
            return (Document)GraphSerializer.Serializer.Deserialize(stream);
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