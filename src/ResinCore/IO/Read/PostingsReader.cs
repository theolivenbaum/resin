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
        protected override IEnumerable<DocumentPosting> Deserialize(byte[] data)
        {
            return Serializer.DeserializePostings(data).ToList();
        }
    }
}