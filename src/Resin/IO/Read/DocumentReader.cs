using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly bool _withCompression;

        public DocumentReader(Stream stream, bool withCompression) : base(stream)
        {
            _withCompression = withCompression;
        }

        protected override Document Deserialize(byte[] data)
        {
            return Serializer.DeserializeDocument(data, _withCompression);
        }
    }
}