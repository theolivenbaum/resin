using DocumentTable;
using Resin.IO;

namespace Resin
{
    public interface IFullTextReadSession:IReadSession
    {
        PostingsReader GetPostingsReader();
        ScoredDocument ReadDocument(DocumentScore score);
    }
}
