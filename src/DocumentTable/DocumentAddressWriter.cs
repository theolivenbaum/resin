using StreamIndex;
using System.IO;

namespace Resin.IO.Write
{
    public class DocumentAddressWriter : BlockWriter<BlockInfo>
    {
        public DocumentAddressWriter(Stream stream) : base(stream)
        {
        }

        protected override byte[] Serialize(BlockInfo block)
        {
            return block.Serialize();
        }
    }
}