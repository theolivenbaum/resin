using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO
{
    public class DiskPostingsReader : PostingsReader
    {
        private readonly Stream _stream;
        private readonly long _offset;

        public DiskPostingsReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public override IList<DocumentPosting> ReadPositionsFromStream(BlockInfo address)
        {
            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            return Serializer.DeserializePostings(_stream, address.Length);
        }

        public override IList<DocumentPosting> ReadTermCountsFromStream(BlockInfo address)
        {
            _stream.Seek(_offset + address.Position, SeekOrigin.Begin);

            return Serializer.DeserializeTermCounts(_stream, address.Length);
        }
    }
}