using DocumentTable;
using Resin.IO;

namespace Resin.Analysis
{
    public struct AnalyzedTerm
    {
        public Term Term { get; private set; }
        public DocumentPosting Posting { get; private set; }

        public AnalyzedTerm(Term term, DocumentPosting posting)
        {
            Term = term;
            Posting = posting;
        }
    }
}