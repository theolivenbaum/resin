using DocumentTable;
using StreamIndex;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public class PostingsReader : BlockReader<IEnumerable<DocumentPosting>>
    {
        public PostingsReader(Stream stream)
            : base(stream)
        {
        }

        public PostingsReader(Stream stream, long offset)
            : base(stream, offset)
        {
        }

        protected override IEnumerable<DocumentPosting> Deserialize(byte[] data)
        {
            return Serializer.DeserializePostings(data).ToList();
        }
    }
}