using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Read
{
    public class PostingsReader : BlockReader<List<DocumentPosting>>
    {
        public PostingsReader(Stream stream)
            : base(stream)
        {
        }
    }
}