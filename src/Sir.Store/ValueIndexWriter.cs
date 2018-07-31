using System;
using System.IO;

namespace Sir.Store
{
    public class ValueIndexWriter
    {
        private readonly Stream _stream;
        private static uint _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);

        public ValueIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public uint Append(long offset, int len, byte dataType)
        {
            var position = _stream.Position;
            var index = (uint)position / _blockSize;

            _stream.Write(BitConverter.GetBytes(offset), 0, sizeof(long));
            _stream.Write(BitConverter.GetBytes(len), 0, sizeof(int));
            _stream.Write(new byte[] { dataType }, 0, sizeof(byte));

            return index;
        }
    }
}
