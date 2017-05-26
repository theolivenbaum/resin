using System.Collections.Generic;

namespace Resin.Analysis
{
    public interface ITokenizer
    {
        IEnumerable<string> Tokenize(string value);
    }
}