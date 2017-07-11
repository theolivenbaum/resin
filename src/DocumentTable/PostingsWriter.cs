using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class PostingsWriter : BlockWriter<List<DocumentPosting>>
    {
        public PostingsWriter(Stream stream) : base(stream)
        {
        }
        protected override int Serialize(List<DocumentPosting> block, Stream stream)
        {
            return block.Serialize(stream);
        }
    }
}