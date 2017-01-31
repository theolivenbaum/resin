using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Resin.IO.Read
{
    public class PostingsReader : IDisposable
    {
        private readonly StreamReader _sr;

        public PostingsReader(StreamReader sr)
        {
            _sr = sr;
        }

        private void Reset()
        {
            _sr.BaseStream.Position = 0;
            _sr.BaseStream.Seek(0, SeekOrigin.Begin);
            _sr.DiscardBufferedData();
        }

        public IList<DocumentPosting> Read(Term term)
        {
            Reset();

            string line;
            var data = string.Empty;

            while ((line = _sr.ReadLine()) != null)
            {
                var parts = line.Split(':');
                var test = new Term(parts[0], parts[1]);

                if (test.Equals(term))
                {
                    data = parts[2];
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(data))
            {
                return new List<DocumentPosting>();
            }

            var bytes = Convert.FromBase64String(data);

            using (var memStream = new MemoryStream(bytes))
            {
                return Deserialize(memStream);
            }
        }

        private IList<DocumentPosting> Deserialize(Stream stream)
        {
            return (IList<DocumentPosting>)BinaryFile.Serializer.Deserialize(stream);
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