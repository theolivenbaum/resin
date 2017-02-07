using System;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Write
{
    public class PostingsWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        public static object SyncRoot = new object();

        public PostingsWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Write(Term term, IEnumerable<DocumentPosting> postings)
        {
            var bytes = Serialize(postings);

            var base64 = Convert.ToBase64String(bytes);

            _writer.WriteLine("{0}:{1}", term, base64);
        }

        private byte[] Serialize(IEnumerable<DocumentPosting> postings)
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