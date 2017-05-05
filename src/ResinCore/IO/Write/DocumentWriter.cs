using System.IO;

namespace Resin.IO.Write
{
    public class DocumentWriter : BlockWriter<Document>
    {
        private readonly Compression _compression;

        public DocumentWriter(Stream stream, Compression compression) : base(stream)
        {
            _compression = compression;
        }

        protected override byte[] Serialize(Document document)
        {
            return document.Serialize(_compression);
        }
    }
}