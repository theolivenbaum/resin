using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Store the location of a value.
    /// </summary>
    public class ValueIndexWriter
    {
        private readonly Stream _stream;
        private static uint _blockSize = sizeof(long) + sizeof(int) + sizeof(byte);

        public ValueIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public async Task<uint> Append(long offset, int len, byte dataType)
        {
            var position = _stream.Position;
            var index = (uint)position / _blockSize;

            await _stream.WriteAsync(BitConverter.GetBytes(offset), 0, sizeof(long));
            await _stream.WriteAsync(BitConverter.GetBytes(len), 0, sizeof(int));
            await _stream.WriteAsync(new byte[] { dataType }, 0, sizeof(byte));

            return index;
        }
    }
}
