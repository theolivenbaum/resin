using StreamIndex;
using System.Collections.Generic;
using System.IO;

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

        protected override IList<DocumentPosting> Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            return TableSerializer.DeserializePostings(stream, size);
        }
    }
}