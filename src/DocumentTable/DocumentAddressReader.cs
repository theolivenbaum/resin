using StreamIndex;
using System.IO;

namespace DocumentTable
{
    public class DocumentAddressReader : BlockReader<BlockInfo>
    {
        public DocumentAddressReader(Stream stream) : base(stream)
        {
        }

        public DocumentAddressReader(Stream stream, long offset) : base(stream, offset)
        {
        }

        protected override BlockInfo Deserialize(byte[] data)
        {
            return BlockSerializer.DeserializeBlock(data);
        }
    }
}