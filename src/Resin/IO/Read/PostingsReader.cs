using System;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Read
{
    public class PostingsReader : IDisposable
    {
        private readonly StreamReader _sr;

        public PostingsReader(StreamReader sr)
        {
            _sr = sr;
        }

        public IEnumerable<DocumentPosting> Read(Term term)
        {
            var headerBytes = Convert.FromBase64String(_sr.ReadLine());
            var header = DeserializeHeader(headerBytes);

            if (header.ContainsKey(term))
            {
                var position = header[term];

                for (int i = 0; i < position; i++)
                {
                    _sr.ReadLine();
                }

                var bytes = Convert.FromBase64String(_sr.ReadLine());

                using (var memStream = new MemoryStream(bytes))
                {
                    foreach (var posting in Deserialize(memStream))
                    {
                        posting.Field = term.Field;
                        yield return posting;
                    }
                }
            }
        }

        private Dictionary<Term, int> DeserializeHeader(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return (Dictionary<Term, int>) BinaryFile.Serializer.Deserialize(stream);
            }
        }

        private IEnumerable<DocumentPosting> Deserialize(Stream stream)
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