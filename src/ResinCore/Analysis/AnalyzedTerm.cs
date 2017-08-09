using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedTerm
    {
        public Term Term { get; private set; }
        public IList<Posting> Postings { get; private set; }

        public AnalyzedTerm(Term term, IList<Posting> postings)
        {
            Term = term;
            Postings = postings;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Term, Postings.Count);
        }
    }
}