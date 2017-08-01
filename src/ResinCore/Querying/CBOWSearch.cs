using DocumentTable;
using Resin.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.Querying
{
    public class CBOWSearch : Search
    {
        public CBOWSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader) { }

        public void Search(QueryContext ctx, IList<string> tokens)
        {
            var postingsMatrix = new List<DocumentPosting>[tokens.Count];

            for (int index = 0; index < tokens.Count; index++)
            {
                postingsMatrix[index] = new List<DocumentPosting>();

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
                    postingsMatrix[index].AddRange(postings);
                }

            }

            ctx.Scores = Score(postingsMatrix);
        }

        private IList<DocumentScore> Score(IList<DocumentPosting>[] postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var weights = new List<DocumentScore>[postings.Length];

            for (int index = 0; index < weights.Length; index++)
            {
                SetWeights(index, postings, weights);
            }

            Log.DebugFormat("scored phrase in {0}", scoreTime.Elapsed);

            return weights.Sum();
        }

        private void SetWeights(int listIndex, IList<DocumentPosting>[] postings, IList<DocumentScore>[] weights)
        {
            var w = new List<DocumentScore>();
            weights[listIndex] = w;

            var maxDistance = postings.Length - 1;
            var first = postings[listIndex];
            var other = postings[postings.Length - (1 + listIndex)];
            var otherIndex = 0;

            for (int i = 0; i < first.Count; i++)
            {
                var firstPosting = first[i];
                var docHash = Session.ReadDocHash(firstPosting.DocumentId);
                var score = 0;

                if (docHash.IsObsolete)
                {
                    continue;
                }

                if (otherIndex + 1 <= other.Count)
                {
                    var otherPosting = other[otherIndex];
                    var end = other.Count - 1;
                    while (otherIndex < end)
                    {
                        if (otherPosting.DocumentId == firstPosting.DocumentId) break;

                        otherPosting = other[++otherIndex];
                    }

                    if (otherPosting.DocumentId == firstPosting.DocumentId)
                    {
                        var distance = Math.Abs(firstPosting.Position - otherPosting.Position);

                        if (distance <= maxDistance)
                        {
                            score = 1;
                        }
                    }

                    w.Add(
                        new DocumentScore(
                            firstPosting.DocumentId, docHash.Hash, score, Session.Version));
                }

            }
        }
    }
}
