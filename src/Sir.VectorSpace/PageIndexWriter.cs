using System;
using System.IO;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Index segment writer.
    /// </summary>
    public class PageIndexWriter : IDisposable
    {
        private readonly Stream _stream;
        public long Position => _stream.Position;

        public PageIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Put(long offset, long length)
        {
            _stream.Write(BitConverter.GetBytes(offset));
            _stream.Write(BitConverter.GetBytes(length));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}