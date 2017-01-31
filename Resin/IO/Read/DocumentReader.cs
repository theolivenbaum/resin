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

        private void Reset()
        {
            _sr.BaseStream.Position = 0;
            _sr.BaseStream.Seek(0, SeekOrigin.Begin);
            _sr.DiscardBufferedData();
        }

        public Document Get(string docId)
        {
            Reset();

            string line;
            var data = string.Empty;

            while ((line = _sr.ReadLine()) != null)
            {
                var parts = line.Split(':');
                var test = parts[0];

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
            return (Document)BinaryFile.Serializer.Deserialize(stream);
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