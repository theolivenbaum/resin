using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Resin.Documents;

namespace Resin
{
    public class NetworkFullTextReadSession : ReadSession, IFullTextReadSession
    {
        private readonly NetworkPostingsReader _postingsReader;
        private readonly NetworkBlockReader _documentsReader;

        public NetworkFullTextReadSession(
            IPEndPoint postingsEndpoint, IPEndPoint documentsEndpoint, SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) 
            : base(version, docHashReader, addressReader, stream)
        {
            _postingsReader = new NetworkPostingsReader(postingsEndpoint);
            _documentsReader = new NetworkBlockReader(documentsEndpoint);
        }

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            return _postingsReader.ReadTermCounts(addresses);
        }

        public IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses)
        {
            return _postingsReader.ReadPositions(addresses);
        }

        public ScoredDocument ReadDocument(DocumentScore score)
        {
            var address = AddressReader.Read(new BlockInfo(score.DocumentId * BlockSize, BlockSize));
            var documentData = _documentsReader.ReadOverNetwork(address);
            var documentStream = new MemoryStream(documentData);
            var document = DocumentSerializer.DeserializeDocument(documentStream, address.Length, Version.Compression, KeyIndex);

            document.Id = score.DocumentId;
            return new ScoredDocument(document, score.Score);
        }
    }
}
