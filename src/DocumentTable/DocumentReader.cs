using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly Compression _compression;
        private readonly IDictionary<short, string> _keyIndex;

        public DocumentReader(
            Stream stream, Compression compression, IDictionary<short, string> keyIndex) : base(stream)
        {
            _compression = compression;
            _keyIndex = keyIndex;
        }

        protected override Document Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return TableSerializer.DeserializeDocument(stream, size, _compression, _keyIndex);
        }
    }
}