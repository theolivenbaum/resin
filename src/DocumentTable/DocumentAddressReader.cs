using StreamIndex;
using System.IO;

namespace DocumentTable
{
    public class DocumentAddressReader : BlockReader<BlockInfo>
    {
        public DocumentAddressReader(Stream stream) : base(stream)
        {
        }

        public DocumentAddressReader(Stream stream, long offset) : base(stream, offset, true)
        {
        }

        protected override BlockInfo Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            return BlockSerializer.DeserializeBlock(stream);
        }
    }
}