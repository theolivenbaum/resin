using System.Collections.Generic;

namespace Resin
{
    public interface IAnalyzer
    {
        AnalyzedDocument AnalyzeDocument(IDictionary<string, string> document);
        IEnumerable<string> Analyze(string value);
    }
}