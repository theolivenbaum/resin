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

        protected override IList<DocumentPosting> Clone(IList<DocumentPosting> input)
        {
            var result = new List<DocumentPosting>();
            foreach(var p in input)
            {
                result.Add(new DocumentPosting(p.DocumentId, p.Position));
            }
            return result;
        }
    }
}