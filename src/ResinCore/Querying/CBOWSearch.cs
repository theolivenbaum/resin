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
            ref IList<DocumentPosting> list1, 
            IList<DocumentPosting> list2, 
            int maxDistance)
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
            DocumentPosting p1, IList<DocumentPosting> list, int maxDistance)
        {
            var start = -1;
            var count = -1;

            for (int i = 0; i < list.Count; i++)
            {
                var p2 = list[i];

                if (p2.DocumentId < p1.DocumentId)
                {
                    continue;
                }

                if (p2.DocumentId > p1.DocumentId)
                {
                    count = i;
                    break;
                }

                if (start == -1)
                {
                    start = i;
                }

                count = list.Count - start;
            }

            if (start < 0 || count < 0)
            {
                return 0;
            }

            var tree = ToPostingsBST(list, start, count);
            var score = tree.FindNearestNeighbours(p1, maxDistance);

            return score;
        }

        private Node ToPostingsBST(IList<DocumentPosting> sorted, int start, int count)
        {
            if (count == 1)
            {
                return new Node(sorted[start]);
            }

            int mid = (start + count) / 2;
            Node node = new Node(sorted[mid]);

            node.Left = ToPostingsBST(sorted, start, mid - 1);
            node.Right = ToPostingsBST(sorted, mid + 1, count);

            return node;
        }

        [DebuggerDisplay("{Data}")]
        private class Node
        {
            public DocumentPosting Data;
            public Node Left, Right;

            public Node(DocumentPosting data)
            {
                Data = data;
                Left = Right = null;
            }

            public float FindNearestNeighbours(
                DocumentPosting posting, int maxDistance)
            {
                float score = 0;
                var node = this;
                var debugList = new List<DocumentPosting>();
                var stack = new Stack<Node>();

                while (node!= null)
                {
                    var distance = node.Data.Position - posting.Position;

                    if (distance < 0)
                    {
                        distance = Math.Abs(distance) + 1;

                        if (distance > maxDistance)
                        {
                            node = node.Right;
                            continue;
                        }
                    }
                    else if (distance > maxDistance)
                    {
                        node = node.Left;
                        continue;
                    }

                    var s = (float)1 / distance;
                    score += s;
                    debugList.Add(node.Data);

                    if (node.Right != null)
                    {
                        stack.Push(node.Right);
                    }

                    if (node.Left != null)
                    {
                        node = node.Left;
                    }
                    else if (stack.Count > 0)
                    {
                        node = stack.Pop();
                    }
                    else
                    {
                        break;
                    }
                }
                return score;
            }
        }
    }
}
