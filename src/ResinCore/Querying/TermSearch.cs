using DocumentTable;
using Resin.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Resin.Querying
{
    public class TermSearch : Search
    {
        public TermSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader) { }


        public void Search(QueryContext ctx)
        {
            var time = Stopwatch.StartNew();

            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Key))
            {
                if (ctx.Query.Fuzzy)
                {
                    terms = reader.SemanticallyNear(ctx.Query.Value, ctx.Query.Edits(ctx.Query.Value))
                        .ToTerms(ctx.Query.Key);
                }
                else if (ctx.Query.Prefix)
                {
                    terms = reader.StartsWith(ctx.Query.Value)
                        .ToTerms(ctx.Query.Key);
                }
                else
                {
                    terms = reader.IsWord(ctx.Query.Value)
                        .ToTerms(ctx.Query.Key);
                }
            }

            Log.DebugFormat("found {0} matching terms for the query {1} in {2}",
                    terms.Count, ctx.Query, time.Elapsed);

            if (Log.IsDebugEnabled && terms.Count > 1)
            {
                Log.DebugFormat("expanded {0}: {1}",
                    ctx.Query.Value, string.Join(" ", terms.Select(t => t.Word.Value)));
            }

            var postings = GetPostingsList(terms);
            ctx.Scores = Score(postings);
        }
    }
}
