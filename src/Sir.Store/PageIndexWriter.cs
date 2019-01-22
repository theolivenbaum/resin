using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Index segment writer.
    /// </summary>
    public class PageIndexWriter : IDisposable
    {
        private readonly Stream _stream;

        public PageIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public async Task WriteAsync(long offset, long length)
        {
            await _stream.WriteAsync(BitConverter.GetBytes(offset));
            await _stream.WriteAsync(BitConverter.GetBytes(length));
        }

        public void Write(long offset, long length)
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