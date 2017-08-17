using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.Querying
{
    public class TermSearch : Search
    {
        public TermSearch(IFullTextReadSession session, IScoringSchemeFactory scoringFactory)
            : base(session, scoringFactory) { }


        public void Search(QueryContext ctx)
        {
            var time = Stopwatch.StartNew();

            var addresses = new List<BlockInfo>();

            using (var reader = GetTreeReader(ctx.Query.Key))
            {
                if (ctx.Query.Fuzzy)
                {
                    var words = reader.SemanticallyNear(
                        ctx.Query.Value, ctx.Query.Edits(ctx.Query.Value));

                    foreach (var word in words)
                    {
                        addresses.Add(word.PostingsAddress.Value);
                    }
                }
                else if (ctx.Query.Prefix)
                {
                    var words = reader.StartsWith(ctx.Query.Value);

                    foreach (var word in words)
                    {
                        addresses.Add(word.PostingsAddress.Value);
                    }
                }
                else
                {
                    var word = reader.IsWord(ctx.Query.Value);

                    if (word != null)
                    {
                        addresses.Add(word.PostingsAddress.Value);
                    }
                }
            }

            Log.InfoFormat("found {0} matching terms for the query {1} in {2}",
                    addresses.Count, ctx.Query, time.Elapsed);

            var postings = Session.ReadTermCounts(addresses);

            ctx.Scores = Score(postings);
        }
    }
}
