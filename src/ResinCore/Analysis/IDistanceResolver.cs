namespace Resin.Analysis
{
    public interface IDistanceResolver
    {
        bool IsValid(char c, int depth);
        void Put(char c, int depth);
        int GetDistance(string a, string b);
    }
}