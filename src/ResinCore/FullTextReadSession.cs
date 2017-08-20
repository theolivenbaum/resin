using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.IO;
using Resin.Documents;

namespace Resin
{
    public class FullTextReadSession : ReadSession, IFullTextReadSession
    {
        public FullTextReadSession(SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) : base(version, docHashReader, addressReader, stream)
        {
        }

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            return new DiskPostingsReader(Stream, Version.PostingsOffset).ReadTermCounts(addresses);
        }

        public IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses)
        {
            return new DiskPostingsReader(Stream, Version.PostingsOffset).ReadPositions(addresses);
        }

        public ScoredDocument ReadDocument(DocumentScore score)
        {
            var document = ReadDocument(score.DocumentId);
            return new ScoredDocument(document, score.Score);
        }
    }
}
