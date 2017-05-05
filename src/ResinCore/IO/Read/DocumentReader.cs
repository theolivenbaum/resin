using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly Compression _compression;

        public DocumentReader(Stream stream, Compression compression) : base(stream)
        {
            _compression = compression;
        }

        protected override Document Deserialize(byte[] data)
        {
            return Serializer.DeserializeDocument(data, _compression);
        }
    }
}