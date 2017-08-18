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

        public override IList<DocumentPosting> ReadPositionsFromStream(IList<BlockInfo> addresses)
        {
            var result = new List<DocumentPosting>();
            foreach(var address in addresses)
            {
                _stream.Seek(_offset + address.Position, SeekOrigin.Begin);
                result.AddRange(Serializer.DeserializePostings(_stream, address.Length));
            }
            return result;
        }

        public override IList<DocumentPosting> ReadTermCountsFromStream(IList<BlockInfo> addresses)
        {
            var result = new List<DocumentPosting>();
            foreach (var address in addresses)
            {
                _stream.Seek(_offset + address.Position, SeekOrigin.Begin);
                result.AddRange(Serializer.DeserializeTermCounts(_stream, address.Length));
            }
            return result;
        }
    }
}