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
            using (var stream = new MemoryStream(data))
            {
                return (Document)GraphSerializer.Serializer.Deserialize(stream);
            }
        }
    }
}