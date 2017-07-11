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

        protected override IList<DocumentPosting> Deserialize(long offset, int size, Stream stream)
        {
            var data = new byte[size];
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Read(data, 0, size);

            return TableSerializer.DeserializePostings(data).ToList();
        }
    }
}