using DocumentTable;
using Resin.IO;
using System.Collections.Generic;
using System.Linq;

namespace Resin.Querying
{
    public class TermSearch : Search
    {
        public TermSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader) { }


        public void Search(QueryContext ctx)
        {
            Log.DebugFormat("executing {0}", ctx.Query);

            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Field))
            {
                if (ctx.Query.Fuzzy)
                {
                    terms = reader.SemanticallyNear(ctx.Query.Value, ctx.Query.Edits(ctx.Query.Value))
                        .ToTerms(ctx.Query.Field);
                }
                else if (ctx.Query.Prefix)
                {
                    terms = reader.StartsWith(ctx.Query.Value)
                        .ToTerms(ctx.Query.Field);
                }
                else
                {
                    terms = reader.IsWord(ctx.Query.Value)
                        .ToTerms(ctx.Query.Field);
                }
            }

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
