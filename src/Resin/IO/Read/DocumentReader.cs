using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Read
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly string[] _fields;

        public DocumentReader(Stream stream, string[] fields) : base(stream)
        {
            _fields = fields;
        }

        protected override Document Deserialize(byte[] data)
        {
            var values = (string[])GraphSerializer.Serializer.Deserialize(new MemoryStream(data));

            var id = int.Parse(values[0]);
            var dic = new Dictionary<string, string>();

            for (int index = 1; index < values.Length; index++)
            {
                dic[_fields[index - 1]] = values[index];
            }

            return new Document(dic) { Id = id };
        }
    }
}