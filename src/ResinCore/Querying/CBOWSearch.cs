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

        private float ScoreDistanceOfWordsInNDimensions (
            int start, int count, DocumentPosting p1, IList<DocumentPosting> list, int maxDist)
            {
                float score = 0;
                var prevDistance = int.MaxValue;
                var took = 0;

                for (int i = start; i<list.Count; i++)
                {
                    if (took == count) break;

                    var p2 = list[i];
                    took++;

                    if (p2.DocumentId<p1.DocumentId)
                    {
                        continue;
                    }

                    if (p2.DocumentId > p1.DocumentId)
                    {
                        break;
                    }

                    var distance = Math.Abs(p1.Position - p2.Position);

                    if (distance <= maxDist)
                    {
                        var sc = (float)1 / (distance);
                        if (sc > score)
                        {
                            score = sc;
                        }
                    }
                    else if (distance > maxDist)
                    {
                        if (prevDistance<distance)
                        {
                            break;
                        }
                        prevDistance = distance;
                    }
                }
                return score;
            }

        public void Search(QueryContext ctx)
        {
            var tokens = ((PhraseQuery)ctx.Query).Values.Distinct().ToArray();

            var postingsMatrix = new IList<DocumentPosting>[tokens.Length];

            for (int index = 0; index < tokens.Length; index++)
            {
                var timer = Stopwatch.StartNew();

                postingsMatrix[index] = new List<DocumentPosting>();

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

            for (int index = 0; index < postings.Length; index++)
            {
                var maxDistance = weights.Length + index;
                var firstList = postings[0];
                var next = index + 1;

                if (next > weights.Length)
                {
                    break;
                }

                var w = new List<DocumentScore>();
                weights[index] = w;

                var secondList = postings[next];

                DocumentPosting posting1;
                DocHash docHash;
                float score;
                int avg = secondList.Count / 4;
                int leftOver = secondList.Count - (avg * 3);

                for (int firstListIndex = 0; firstListIndex < firstList.Count; firstListIndex++)
                {
                    posting1 = firstList[firstListIndex];
                    score = 0;

                    if (secondList.Count > 3)
                    {
                        score = ScoreDistanceOfWordsInNDimensions(
                            avg * 3, leftOver, posting1, secondList, maxDistance);

                        if (score == 0)
                            score = ScoreDistanceOfWordsInNDimensions(
                                avg * 0, avg, posting1, secondList, maxDistance);

                        if (score == 0)
                            score = ScoreDistanceOfWordsInNDimensions(
                                avg * 2, avg, posting1, secondList, maxDistance);

                        if (score == 0)
                            score = ScoreDistanceOfWordsInNDimensions(
                                avg * 1, avg, posting1, secondList, maxDistance);
                    }
                    else
                    {
                        score = ScoreDistanceOfWordsInNDimensions(
                            0, secondList.Count, posting1, secondList, maxDistance);
                    }
                    if (score > 0)
                    {
                        docHash = Session.ReadDocHash(posting1.DocumentId);

                        if (!docHash.IsObsolete)
                        {
                            w.Add(
                                new DocumentScore(
                                    posting1.DocumentId, docHash.Hash, score, Session.Version));
                        }

                    }
                    Log.DebugFormat("document ID {0} scored {1}",
                        posting1.DocumentId, score);
                }

                Log.DebugFormat("produced {0} scores in {1}",
                    w.Count, timer.Elapsed);
            }
            

            

        }
    }
}
