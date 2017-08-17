using DocumentTable;
using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin
{
    public class NetworkFullTextReadSession : ReadSession, IFullTextReadSession
    {
        public NetworkFullTextReadSession(
            SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) 
            : base(version, docHashReader, addressReader, stream)
        {
        }

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            return new PostingsReader(Stream, Version.PostingsOffset).ReadTermCounts(addresses);
        }

        public IList<IList<DocumentPosting>> ReadMany(IList<IList<BlockInfo>> addresses)
        {
            return new PostingsReader(Stream, Version.PostingsOffset).ReadMany(addresses);
        }

        public IList<DocumentPosting> Read(IList<BlockInfo> addresses)
        {
            return new PostingsReader(Stream, Version.PostingsOffset).Read(addresses);
        }

        public ScoredDocument ReadDocument(DocumentScore score)
        {
            var docAddress = AddressReader.Read(
                new BlockInfo(score.DocumentId * BlockSize, BlockSize));

            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);

            var keyIndex = DocumentSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, keyIndex, leaveOpen: true))
            {
                var document = documentReader.Read(docAddress);
                document.Id = score.DocumentId;
                return new ScoredDocument(document, score.Score);
            }
        }
    }
}
