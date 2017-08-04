using DocumentTable;
using Resin.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Resin.Querying
{
    public class CBOWSearch : Search
    {
        public CBOWSearch(IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
            : base(session, scoringFactory, postingsReader)
        {
        }

        public void Search(QueryContext ctx)
        {
            var tokens = ((PhraseQuery)ctx.Query).Values.Distinct().ToArray();

            var postingsMatrix = new IList<DocumentPosting>[tokens.Length];

            for (int index = 0; index < tokens.Length; index++)
            {
                var timer = Stopwatch.StartNew();

                var token = tokens[index];

                IList<Term> terms;

                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    terms = reader.IsWord(token)
                            .ToTerms(ctx.Query.Field);
                }

                var postings = GetPostingsList(terms[0]);

                Log.DebugFormat("read postings for {0} in {1}", terms[0].Word.Value, timer.Elapsed);

                postingsMatrix[index] = postings;
            }

            ctx.Scores = Score(postingsMatrix);
        }

        private IList<DocumentScore> Score(IList<DocumentPosting>[] postings)
        {
            if (postings.Length == 1)
            {
                return Score(postings[0]);
            }

            var weights = new List<DocumentScore>[postings.Length-1];

            SetWeights(postings, weights);

            return weights.Sum();
        }

        private void SetWeights(IList<DocumentPosting>[] postings, IList<DocumentScore>[] weights)
        {
            Log.Debug("scoring.. ");

            var timer = Stopwatch.StartNew();

            var first = postings[0];
            var maxDistance = postings.Length;
            var firstScoreList = Score(ref first, postings[1], maxDistance);

            weights[0] = firstScoreList;

            for (int index = 2; index < postings.Length; index++)
            {
                maxDistance++;

                var scores = Score(ref first, postings[index], maxDistance);

                weights[index-1] = scores;

                Log.DebugFormat("produced {0} scores in {1}",
                    scores.Count, timer.Elapsed);
            }
        }

        private IList<DocumentScore> Score (
            ref IList<DocumentPosting> list1, IList<DocumentPosting> list2, int maxDistance)
        {
            var scores = new List<DocumentScore>();
            DocumentPosting posting;
            float score;
            var nearPostings = new List<DocumentPosting>();

            for (int postingIndex = 0; postingIndex < list1.Count; postingIndex++)
            {
                posting = list1[postingIndex];

                score = ScoreDistanceOfWordsInNDimensions(
                    posting, list2, maxDistance);

                if (score > 0)
                {
                    scores.Add(
                            new DocumentScore(
                                posting.DocumentId, score, Session.Version));

                    nearPostings.Add(posting);

                    Log.DebugFormat("document ID {0} scored {1}",
                    posting.DocumentId, score);
                }
            }
            list1 = nearPostings;
            return scores;
        }

        private float ScoreDistanceOfWordsInNDimensions(
            DocumentPosting p1, IList<DocumentPosting> list, int maxDist)
        {
            float score = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var p2 = list[i];

                if (p2.DocumentId < p1.DocumentId)
                {
                    continue;
                }

                if (p2.DocumentId > p1.DocumentId)
                {
                    break;
                }

                var distance = p2.Position - p1.Position;

                if (distance < 0)
                {
                    distance = Math.Abs(distance-1);
                }

                if (distance <= maxDist)
                {
                    float sc = (float)1 / distance;
                    if (sc > score)
                    {
                        score = sc;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return score;
        }
    }
}
