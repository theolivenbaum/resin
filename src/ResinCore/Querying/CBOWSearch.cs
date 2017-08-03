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

        public void Search(QueryContext ctx)
        {
            var tokens = ((PhraseQuery)ctx.Query).Values;

            var postingsMatrix = new IList<DocumentPosting>[tokens.Count];

            for (int index = 0; index < tokens.Count; index++)
            {
                postingsMatrix[index] = new List<DocumentPosting>();

                var token = tokens[index];
                IList<Term> terms;

                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    terms = reader.IsWord(token)
                            .ToTerms(ctx.Query.Field);
                }

                var postings = GetPostingsList(terms[0]);

                postingsMatrix[index] = postings;
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
            var firstList = postings[listIndex];
            var secondList = postings[postings.Length - (1 + listIndex)];

            DocumentPosting posting1;
            DocumentPosting posting2;
            DocHash docHash;
            int score;

            for (int index = 0; index < firstList.Count; index++)
            {
                posting1 = firstList[index];
                docHash = Session.ReadDocHash(posting1.DocumentId);
                score = 0;

                if (docHash.IsObsolete)
                {
                    continue;
                }

                for (int secondIndex = 0; secondIndex < secondList.Count; secondIndex++)
                {
                    posting2 = secondList[secondIndex];

                    if (posting2.DocumentId < posting1.DocumentId)
                    {
                        continue;
                    }

                    if (posting2.DocumentId > posting1.DocumentId)
                    {
                        break;
                    }

                    var distance = Math.Abs(posting1.Position - posting2.Position);

                    if (distance <= maxDistance)
                    {
                        score = 1;
                    }
                }

                if (score > 0)
                {
                    w.Add(
                    new DocumentScore(
                        posting1.DocumentId, docHash.Hash, score, Session.Version));
                }
            }

        }
    }
}
