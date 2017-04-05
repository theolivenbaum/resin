using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        public DocumentReader(Stream stream) : base(stream)
        {
        }

        protected override Document Deserialize(byte[] data)
        {
            return Serializer.DeserializeDocument(data);
        }
    }
}