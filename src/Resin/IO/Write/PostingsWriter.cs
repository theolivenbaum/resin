using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Write
{
    public class PostingsWriter : IDisposable
    {
        private readonly StreamWriter _writer;

        public PostingsWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Write(Dictionary<Term, List<DocumentPosting>> postings)
        {
            var index = 0;
            var header = postings.Keys.ToList().ToDictionary(x=>x, x=>index++);
            var headerBytes = Serialize(header);

            _writer.WriteLine(Convert.ToBase64String(headerBytes));
            
            foreach (var term in header.Keys)
            {
                var bytes = Serialize(postings[term]);

                _writer.WriteLine(Convert.ToBase64String(bytes));
            }
        }

        private byte[] Serialize(Dictionary<Term, int> header)
        {
            using (var stream = new MemoryStream())
            {
                BinaryFile.Serializer.Serialize(stream, header);
                return stream.ToArray();
            }
        }

        private byte[] Serialize(List<DocumentPosting> postings)
        {
            using (var stream = new MemoryStream())
            {
                BinaryFile.Serializer.Serialize(stream, postings);
                return stream.ToArray();
            }
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
        }
    }
}