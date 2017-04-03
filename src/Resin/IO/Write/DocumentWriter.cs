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
            var values = block.Fields.OrderBy(x => x.Key).Select(x => x.Value).ToList();
            
            values.Insert(0, block.Id.ToString(CultureInfo.InvariantCulture));

            using (var ms = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(ms, values.ToArray());
                return ms.ToArray();
            }
        }
    }
}