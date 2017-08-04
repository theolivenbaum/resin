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
                var postingsList = postings[0];
                var next = index + 1;

                if (next > weights.Length)
                {
                    break;
                }

                var w = new List<DocumentScore>();
                weights[index] = w;

                var secondList = postings[next];

                DocumentPosting posting;
                DocHash docHash;
                float score;
                int avg = secondList.Count / 4;
                int leftOver = secondList.Count - (avg * 3);

                for (int postingIndex = 0; postingIndex < postingsList.Count; postingIndex++)
                {
                    posting = postingsList[postingIndex];

                    score = ScoreDistanceOfWordsInNDimensions(
                        posting, secondList, maxDistance);

                    if (score > 0)
                    {
                        docHash = Session.ReadDocHash(posting.DocumentId);

                        if (!docHash.IsObsolete)
                        {
                            w.Add(
                                new DocumentScore(
                                    posting.DocumentId, docHash.Hash, score, Session.Version));
                        }

                    }
                    Log.DebugFormat("document ID {0} scored {1}",
                        posting.DocumentId, score);
                }

                Log.DebugFormat("produced {0} scores in {1}",
                    w.Count, timer.Elapsed);
            }
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

                var distance = Math.Abs(p1.Position - p2.Position);

                if (distance <= maxDist)
                {
                    var sc = (float)1 / distance;
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
