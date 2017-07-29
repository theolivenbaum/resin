using DocumentTable;
using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedTerm
    {
        public Term Term { get; private set; }
        public IList<DocumentPosting> Postings { get; private set; }

        public AnalyzedTerm(Term term, IList<DocumentPosting> postings)
        {
            Term = term;
            Postings = postings;
        }
    }
}