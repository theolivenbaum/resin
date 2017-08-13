using DocumentTable;
using Resin.IO;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.Querying
{
    public class RangeSearch : Search
    {
        public RangeSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader) { }


        public void Search(QueryContext ctx, string valueUpperBound)
        {
            var time = Stopwatch.StartNew();

            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Key))
            {
                terms = reader.Range(ctx.Query.Value, valueUpperBound)
                        .ToTerms(ctx.Query.Key);
            }

            Log.DebugFormat("found {0} matching terms for the query {1} in {2}",
                    terms.Count, ctx.Query, time.Elapsed);

            var postings = GetPostingsList(terms);
            ctx.Scores = Score(postings);
        }
    }
}
