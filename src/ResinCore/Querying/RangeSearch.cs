using Resin.IO;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.Querying
{
    public class RangeSearch : Search
    {
        public RangeSearch(IFullTextReadSession session, IScoringSchemeFactory scoringFactory)
            : base(session, scoringFactory) { }


        public void Search(QueryContext ctx, string valueUpperBound)
        {
            var time = Stopwatch.StartNew();

            var addresses = new List<BlockInfo>();

            using (var reader = GetTreeReader(ctx.Query.Key))
            {
                var words = reader.Range(ctx.Query.Value, valueUpperBound);

                foreach (var word in words)
                {
                    addresses.Add(word.PostingsAddress.Value);
                }
            }

            Log.InfoFormat("found {0} matching terms for the query {1} in {2}",
                    addresses.Count, ctx.Query, time.Elapsed);

            var postings = Session.ReadTermCounts(addresses);

            ctx.Scores = Score(postings);
        }
    }
}
