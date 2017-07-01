namespace Resin.Analysis
{
    public interface IDistanceAutomaton
    {
        bool IsValid(char c, int depth);
        void Put(char c, int depth);
    }
}
