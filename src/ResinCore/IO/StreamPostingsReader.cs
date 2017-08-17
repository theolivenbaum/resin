using StreamIndex;
using System.IO;

namespace Resin.IO
{
    public class StreamPostingsReader
    {
        private readonly Stream _stream;
        private readonly long _offset;

        public StreamPostingsReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public byte[] ReadPositionsFromStream(BlockInfo address)
        {
            var buffer = new byte[address.Length];

            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            _stream.Read(buffer, 0, buffer.Length);

            return buffer;
        }

        public byte[] ReadTermCountsFromStream(BlockInfo address)
        {
            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            var termCounts = Serializer.DeserializeTermCounts(_stream, address.Length);

            var data = termCounts.Serialize();

            return data;
        }
    }
}