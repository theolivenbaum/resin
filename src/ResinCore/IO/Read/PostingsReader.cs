using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public class PostingsReader : AutoResettingBlockReader<IEnumerable<DocumentPosting>>
    {
        public PostingsReader(Stream stream, bool leaveOpen = false)
            : base(stream, leaveOpen)
        {
        }
        protected override IEnumerable<DocumentPosting> Deserialize(byte[] data)
        {
            return Serializer.DeserializePostings(data).ToList();
        }
    }
}