using System.Collections.Generic;
using DocumentTable;

namespace Resin.Analysis
{
    public interface IAnalyzer
    {
        AnalyzedDocument AnalyzeDocument(Document document);
        IEnumerable<string> Analyze(string value);
    }
}