using StreamIndex;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocumentTable
{
    public class PostingsReader : BlockReader<IList<DocumentPosting>>
    {
        public PostingsReader(Stream stream)
            : base(stream)
        {
        }

        public PostingsReader(Stream stream, long offset)
            : base(stream, offset)
        {
        }

        protected override IList<DocumentPosting> Deserialize(byte[] data)
        {
            return TableSerializer.DeserializePostings(data).ToList();
        }
    }
}