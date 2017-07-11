using StreamIndex;
using System.IO;

namespace DocumentTable
{
    public class DocumentAddressWriter : BlockWriter<BlockInfo>
    {
        public DocumentAddressWriter(Stream stream) : base(stream)
        {
        }

        protected override int Serialize(BlockInfo block, Stream stream)
        {
            return block.Serialize(stream);
        }
    }
}