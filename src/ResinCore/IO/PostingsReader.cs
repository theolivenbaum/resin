using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin.IO
{
    public class PostingsReader : BlockReader<IList<DocumentPosting>>
    {
        public PostingsReader(Stream stream)
            : base(stream)
        {
        }

        public PostingsReader(Stream stream, long offset)
            : base(stream, offset, true)
        {
        }

        protected override IList<DocumentPosting> Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            return Serializer.DeserializePostings(stream, size);
        }
    }
}