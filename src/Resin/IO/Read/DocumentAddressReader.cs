using System.IO;

namespace Resin.IO.Read
{
    public class DocumentAddressReader : BlockReader<BlockInfo>
    {
        public DocumentAddressReader(Stream stream) : base(stream)
        {
        }
    }
}