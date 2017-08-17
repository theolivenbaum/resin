using System;
using System.IO;

namespace DocumentTable
{
    public class DocHashReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly long _offset;
        private readonly bool _leaveOpen;

        public DocHashReader(string fileName)
            : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), 0, false)
        {
        }

        public DocHashReader(Stream stream, long offset, bool leaveOpen = true)
        {
            _stream = stream;
            _offset = offset;
            _leaveOpen = leaveOpen;
        }

        public DocHash Read(int docId)
        {
            var pos = docId*DocumentSerializer.SizeOfDocHash() + _offset;

            _stream.Seek(pos, SeekOrigin.Begin);

            var hash = DocumentSerializer.DeserializeDocHash(_stream);

            return hash;
        }

        public void Dispose()
        {
            if (!_leaveOpen) _stream.Dispose();
        }
    }
}