using StreamIndex;
using System.IO;

namespace Resin.IO
{
    public class StreamBlockReader
    {
        private readonly Stream _stream;
        private readonly long _offset;

        public StreamBlockReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public byte[] ReadFromStream(BlockInfo address)
        {
            var buffer = new byte[address.Length];

            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            _stream.Read(buffer, 0, buffer.Length);

            return buffer;
        }
    }
}