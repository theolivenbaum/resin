using System.Globalization;
using System.IO;
using System.Linq;

namespace Resin.IO.Write
{
    public class DocumentWriter : BlockWriter<Document>
    {
        public DocumentWriter(Stream stream) : base(stream)
        {
        }

        protected override byte[] Serialize(Document block)
        {
            using (var ms = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(ms, block);
                return ms.ToArray();
            }
        }
    }
}