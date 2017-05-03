using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly bool _deflate;

        public DocumentReader(Stream stream, bool deflate) : base(stream)
        {
            _deflate = deflate;
        }

        protected override Document Deserialize(byte[] data)
        {
            return Serializer.DeserializeDocument(data, _deflate);
        }
    }
}