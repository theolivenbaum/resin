using DocumentTable;
using StreamIndex;
using System.Collections.Generic;

namespace Resin
{
    public interface IFullTextReadSession:IReadSession
    {
        IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses);
        IList<IList<DocumentPosting>> ReadMany(IList<IList<BlockInfo>> addresses);
        IList<DocumentPosting> Read(IList<BlockInfo> addresses);
        ScoredDocument ReadDocument(DocumentScore score);
    }
}
