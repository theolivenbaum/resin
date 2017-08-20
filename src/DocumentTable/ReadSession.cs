using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin.Documents
{
    public class ReadSession : IReadSession
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(ReadSession));

        protected readonly DocHashReader DocHashReader;
        protected readonly BlockInfoReader AddressReader;
        protected readonly int BlockSize;

        public SegmentInfo Version { get; set; }
        public Stream Stream { get; protected set; }
        public IDictionary<short, string> KeyIndex { get; protected set; }

        public ReadSession(
            SegmentInfo version, 
            DocHashReader docHashReader, 
            BlockInfoReader addressReader, 
            Stream stream)
        {
            Version = version;

            Stream = stream;
            DocHashReader = docHashReader;
            AddressReader = addressReader;
            BlockSize = BlockSerializer.SizeOfBlock();
            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);
            KeyIndex = DocumentSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);
        }

        public DocHash ReadDocHash(int docId)
        {
            return DocHashReader.Read(docId);
        }

        public Document ReadDocument(int documentId)
        {
            var address = AddressReader.Read(
                new BlockInfo(documentId * BlockSize, BlockSize));

            var documentsOffset = Version.KeyIndexOffset + Version.KeyIndexSize;

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, KeyIndex, documentsOffset, leaveOpen: true))
            {
                var document = documentReader.Read(address);
                document.Id = documentId;
                return document;
            }
        }

        public virtual void Dispose()
        {
            AddressReader.Dispose();
            DocHashReader.Dispose();
        }
    }
}
