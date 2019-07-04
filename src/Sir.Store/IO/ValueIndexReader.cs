using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Find out there in the value stream a value is stored, by supplying a value ID.
    /// </summary>
    public class ValueIndexReader : IDisposable
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);

        public ValueIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public async Task<(long offset, int len, byte dataType)> ReadAsync(long id)
        {
            var offset = id * _blockSize;

            _stream.Seek(offset, SeekOrigin.Begin);

            var buf = new byte[_blockSize];
            var read = await _stream.ReadAsync(buf, 0, _blockSize);

            if (read != _blockSize)
            {
                throw new InvalidDataException();
            }

            return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)), buf[_blockSize - 1]);
        }

        public (long offset, int len, byte dataType) Read(long id)
        {
            var offset = id * _blockSize;

            _stream.Seek(offset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[_blockSize];
            var read =  _stream.Read(buf);

            if (read != _blockSize)
            {
                throw new InvalidDataException();
            }

            return (BitConverter.ToInt64(buf.Slice(0)), BitConverter.ToInt32(buf.Slice(sizeof(long))), buf[_blockSize - 1]);
        }
    }
}
