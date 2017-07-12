using System.Collections.Generic;
using DocumentTable;

namespace Resin.Analysis
{
    public interface IAnalyzer
    {
        IList<AnalyzedTerm> AnalyzeDocument(Document document);
        IList<string> Analyze(string value);
    }
}