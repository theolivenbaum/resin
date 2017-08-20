using Resin.Documents;
using System.Collections.Generic;

namespace Resin.Analysis
{
    public interface IAnalyzer
    {
        IList<AnalyzedTerm> AnalyzeDocument(DocumentTableRow document);
        IList<string> Analyze(string value);
    }
}