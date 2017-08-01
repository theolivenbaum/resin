using System.IO;

namespace StreamIndex
{
    public class BlockInfoWriter : BlockWriter<BlockInfo>
    {
        public BlockInfoWriter(Stream stream) : base(stream)
        {
        }

        protected override int Serialize(BlockInfo block, Stream stream)
        {
            return block.Serialize(stream);
        }
    }
}