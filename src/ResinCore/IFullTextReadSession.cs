using StreamIndex;
using System.Collections.Generic;
using Resin.Documents;

namespace Resin
{
    public interface IFullTextReadSession : IReadSession
    {
        IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses);
        IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses);
        ScoredDocument ReadDocument(DocumentScore score);
    }
}
