using System.Collections.Generic;
using Resin.IO;

namespace Resin
{
    public interface IAnalyzer
    {
        AnalyzedDocument AnalyzeDocument(Document document);
        IEnumerable<string> Analyze(string value);
    }
}