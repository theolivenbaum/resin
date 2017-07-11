using StreamIndex;
using System.IO;

namespace DocumentTable
{
    public class DocumentWriter : BlockWriter<DocumentTableRow>
    {
        private readonly Compression _compression;

        public DocumentWriter(Stream stream, Compression compression) : base(stream)
        {
            _compression = compression;
        }

        protected override int Serialize(DocumentTableRow document, Stream stream)
        {
            return document.Serialize(stream, _compression);
        }
    }
}