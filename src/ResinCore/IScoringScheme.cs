using DocumentTable;

namespace Resin
{
    public interface IScoringScheme
    {
        double Score(DocumentPosting posting);
    }
}