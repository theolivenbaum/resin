using StreamIndex;
using System.IO;

namespace DocumentTable
{
    public class DocumentAddressReader : BlockReader<BlockInfo>
    {
        public DocumentAddressReader(Stream stream) : base(stream)
        {
        }

        protected override BlockInfo Deserialize(byte[] data)
        {
            return BlockSerializer.DeserializeBlock(data);
        }
    }
}