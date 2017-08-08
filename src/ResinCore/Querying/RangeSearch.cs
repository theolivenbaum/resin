using DocumentTable;
using Resin.IO;
using System.Collections.Generic;

namespace Resin.Querying
{
    public class RangeSearch : Search
    {
        public RangeSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader) { }


        public void Search(QueryContext ctx, string valueUpperBound)
        {
            Log.DebugFormat("executing {0}", ctx.Query);

            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Field))
            {
                terms = reader.Range(ctx.Query.Value, valueUpperBound)
                        .ToTerms(ctx.Query.Field);
            }

            var postings = GetPostingsList(terms);
            ctx.Scores = Score(postings);
        }
    }
}
