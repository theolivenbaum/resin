using System.IO;
using CSharpTest.Net.Serialization;

namespace Resin.IO
{
    public class PostingSerializer : ISerializer<DocumentPosting>
    {
        public void WriteTo(DocumentPosting value, Stream stream)
        {
            PrimitiveSerializer.Int32.WriteTo(value.DocumentId, stream);
            PrimitiveSerializer.Int32.WriteTo(value.Count, stream);
        }

        public DocumentPosting ReadFrom(Stream stream)
        {
            var docId = PrimitiveSerializer.Int32.ReadFrom(stream);
            var count = PrimitiveSerializer.Int32.ReadFrom(stream);

            return new DocumentPosting(docId, count);
        }
    }
}