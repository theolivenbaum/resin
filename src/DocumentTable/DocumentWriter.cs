using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class DocumentWriter : BlockWriter<IDictionary<short, Field>>
    {
        private readonly Compression _compression;

        public DocumentWriter(Stream stream, Compression compression) : base(stream)
        {
            _compression = compression;
        }

        protected override int Serialize(IDictionary<short, Field> document, Stream stream)
        {
            return document.Serialize(_compression, stream);
        }
    }
}