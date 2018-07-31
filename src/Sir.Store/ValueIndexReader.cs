using System;
using System.IO;

namespace Sir.Store
{
    public class ValueIndexReader
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);

        public ValueIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int len, byte dataType) Read(uint index)
        {
            var offs = index * _blockSize;

            _stream.Seek(offs, SeekOrigin.Begin);

            var buf = new byte[_blockSize];
            var read = _stream.Read(buf, 0, _blockSize);

            if (read != _blockSize)
            {
                throw new InvalidDataException();
            }

            return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)), buf[_blockSize-1]);
        }
    }
}
