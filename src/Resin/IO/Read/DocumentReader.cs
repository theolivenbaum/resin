using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        public DocumentReader(Stream stream) : base(stream)
        {
        }
    }
}