using System.IO;

namespace Resin.IO.Write
{
    public class DocumentWriter : BlockWriter<Document>
    {
        private readonly bool _withCompression;

        public DocumentWriter(Stream stream, bool withCompression) : base(stream)
        {
            _withCompression = withCompression;
        }

        protected override byte[] Serialize(Document document)
        {
            return document.Serialize(_withCompression);
        }
    }
}