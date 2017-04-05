using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        public DocumentReader(Stream stream, string[] fields) : base(stream)
        {
        }

        protected override Document Deserialize(byte[] data)
        {
            var doc = (Document)GraphSerializer.Serializer.Deserialize(new MemoryStream(data));

            return doc;
        }
    }
}