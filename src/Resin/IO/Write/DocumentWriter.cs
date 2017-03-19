using System.IO;

namespace Resin.IO.Write
{
    public class DocumentWriter : BlockWriter<Document>
    {
        public DocumentWriter(Stream stream) : base(stream)
        {
        }
    }
}