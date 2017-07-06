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
        protected override byte[] Serialize(List<DocumentPosting> block)
        {
            return block.Serialize();
        }
    }
}