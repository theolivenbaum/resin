using DocumentTable;
using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Resin
{
    public class NetworkFullTextReadSession : ReadSession, IFullTextReadSession
    {
        private readonly IPEndPoint _ip;

        public NetworkFullTextReadSession(
            IPEndPoint ip, SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) 
            : base(version, docHashReader, addressReader, stream)
        {
            _ip = ip;
        }

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            return new NetworkPostingsReader(_ip).ReadTermCounts(addresses);
        }

        public IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses)
        {
            return new NetworkPostingsReader(_ip).ReadPositions(addresses);
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
