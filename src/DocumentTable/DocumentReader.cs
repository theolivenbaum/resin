using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class DocumentReader : BlockReader<Document>
    {
        private readonly Compression _compression;
        private readonly IDictionary<short, string> _keyIndex;
        private readonly long _offset;

        public DocumentReader(
            Stream stream, Compression compression, IDictionary<short, string> keyIndex, bool leaveOpen) 
            : base(stream, leaveOpen)
        {
            _compression = compression;
            _keyIndex = keyIndex;
            _offset = stream.Position;
        }

        protected override Document Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(_offset + offset, SeekOrigin.Begin);

            return TableSerializer.DeserializeDocument(stream, size, _compression, _keyIndex);
        }

        protected override Document Clone(Document input)
        {
            return new Document(input.Id, input.Fields);
        }
    }
}