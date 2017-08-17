using DocumentTable;
using StreamIndex;
using System.Collections.Generic;

namespace Resin
{
    public interface IFullTextReadSession:IReadSession
    {
        IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses);
        IList<IList<DocumentPosting>> ReadPositions(IList<IList<BlockInfo>> addresses);
        ScoredDocument ReadDocument(DocumentScore score);
    }
}
