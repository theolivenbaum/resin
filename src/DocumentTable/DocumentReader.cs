using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin.Documents
{
    public class DocumentReader : BlockReader<DocumentTableRow>
    {
        private readonly Compression _compression;
        private readonly IDictionary<short, string> _keyIndex;
        private readonly long _offset;

        public DocumentReader(
            Stream stream, Compression compression, IDictionary<short, string> keyIndex, long offset, bool leaveOpen) 
            : base(stream, leaveOpen)
        {
            _compression = compression;
            _keyIndex = keyIndex;
            _offset = offset;
        }

        protected override DocumentTableRow Deserialize(long offset, int size, Stream stream)
        {
            stream.Seek(_offset + offset, SeekOrigin.Begin);

            return DocumentSerializer.DeserializeDocument(stream, size, _compression, _keyIndex);
        }
    }
}