using System;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Find out there in the value stream a value is stored, by supplying a value ID.
    /// </summary>
    public class ValueIndexReader
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);
        private readonly object _sync = new object();

        public ValueIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int len, byte dataType) Read(long id)
        {
            var offset = id * _blockSize;

            lock (_sync)
            {
                _stream.Seek(offset, SeekOrigin.Begin);

                var buf = new byte[_blockSize];
                var read = _stream.Read(buf, 0, _blockSize);

                if (read != _blockSize)
                {
                    throw new InvalidDataException();
                }

                return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)), buf[_blockSize - 1]);
            }
        }
    }
}
