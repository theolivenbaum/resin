using DocumentTable;
using Resin.IO;
using System.Collections.Generic;

namespace Resin.Querying
{
    public class TermFrequencyPhraseSearch : Search
    {
        public TermFrequencyPhraseSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader) 
            : base(session, scoringFactory, postingsReader) { }

        public void Search(QueryContext ctx, IList<string>tokens)
        {
            var scoreMatrix = new IList<DocumentScore>[tokens.Count];

            for (int index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                IList<Term> terms;

                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    if (ctx.Query.Fuzzy)
                    {
                        terms = reader.SemanticallyNear(token, ctx.Query.Edits(token))
                            .ToTerms(ctx.Query.Field);
                    }
                    else if (ctx.Query.Prefix)
                    {
                        terms = reader.StartsWith(token)
                            .ToTerms(ctx.Query.Field);
                    }
                    else
                    {
                        terms = reader.IsWord(token)
                            .ToTerms(ctx.Query.Field);
                    }
                }

                var postings = terms.Count > 0 ? ReadPostings(terms).Sum() : null;

                if (postings != null)
                {
                    var scores = Score(postings);
                    scoreMatrix[index] = scores;
                }
            }

            ctx.Scores = scoreMatrix.Sum();
        }
    }
}
