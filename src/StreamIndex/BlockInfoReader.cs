using System.IO;

namespace StreamIndex
{
    public class BlockInfoReader : BlockReader<BlockInfo>
    {
        public BlockInfoReader(Stream stream) : base(stream)
        {
        }

        public BlockInfoReader(Stream stream, long offset) : base(stream, offset, true)
        {
        }

        protected override BlockInfo Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            return BlockSerializer.DeserializeBlock(stream);
        }
    }
}