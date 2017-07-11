using System;
using System.IO;

namespace DocumentTable
{
    public class DocHashReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly long _offset;

        public DocHashReader(string fileName)
            : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), 0)
        {
        }

        public DocHashReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public DocHash Read(int docId)
        {
            var pos = docId*TableSerializer.SizeOfDocHash() + _offset;

            _stream.Seek(pos, SeekOrigin.Begin);

            var hash = TableSerializer.DeserializeDocHash(_stream).Value;

            return hash;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}