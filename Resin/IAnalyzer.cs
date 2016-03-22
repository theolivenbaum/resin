using System.Collections.Generic;

namespace Resin
{
    public interface IAnalyzer
    {
        IEnumerable<string> Analyze(string value);
    }
}