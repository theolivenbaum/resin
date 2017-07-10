using System.Collections.Generic;

namespace Resin.Analysis
{
    public interface ITokenizer
    {
        void Tokenize(string value, List<(int Start, int Length)> tokens);
    }
}