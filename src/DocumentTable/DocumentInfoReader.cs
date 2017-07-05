using System;
using System.IO;

namespace DocumentTable
{
    public class DocumentInfoReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly long _offset;

        public DocumentInfoReader(string fileName):this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), 0)
        {
        }

        public DocumentInfoReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public DocumentInfo Read(int docId)
        {
            var distance = docId*TableSerializer.SizeOfDocHash() + _offset;

            _stream.Seek(distance, SeekOrigin.Begin);

            var hash = TableSerializer.DeserializeDocHash(_stream).Value;

            return hash;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}