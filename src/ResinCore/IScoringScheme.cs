namespace Resin
{
    public interface IScoringScheme
    {
        double Score(int termCount);
    }
}