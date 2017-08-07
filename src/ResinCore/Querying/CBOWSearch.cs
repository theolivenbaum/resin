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
            var trees = new Node[postings.Length];

            for (int i = 0;i<postings.Length;i++)
            {
                trees[i] = ToBST(postings[i], 0, postings[i].Count-1);
            }

            if (postings.Length == 1)
            {
                return Score(postings[0]);
            }

            var weights = new List<DocumentScore>[postings.Length-1];

            SetWeights(postings, trees, weights);

            return weights.Sum();
        }

        private void SetWeights(IList<DocumentPosting>[] postings, Node[] trees, IList<DocumentScore>[] weights)
        {
            Log.Debug("scoring.. ");

            var timer = Stopwatch.StartNew();

            var first = postings[0];
            var maxDistance = trees.Length;
            var firstScoreList = Score(first, trees[1], maxDistance);

            weights[0] = firstScoreList;

            for (int index = 2; index < trees.Length; index++)
            {
                maxDistance++;

                var scores = Score(first, trees[index], maxDistance);

                weights[index-1] = scores;

                Log.DebugFormat("produced {0} scores in {1}",
                    scores.Count, timer.Elapsed);
            }
        }

        private IList<DocumentScore> Score (
            IList<DocumentPosting> list1, 
            Node list2, 
            int maxDistance)
        {
            var scores = new List<DocumentScore>();
            var documentId = -1;
            Node subTree = null;

            foreach (var posting in list1)
            {
                if (documentId != posting.DocumentId)
                {
                    if (!list2.TryGetSubTree(posting.DocumentId, out subTree))
                    {
                        continue;
                    }
                    documentId = posting.DocumentId;
                }

                var score = subTree.FindNearPostings(posting, maxDistance);

                if (score > 0)
                {
                    scores.Add(
                            new DocumentScore(
                                posting.DocumentId, score, Session.Version));

                    Log.DebugFormat("document ID {0} scored {1}",
                        posting.DocumentId, score);
                }
            }
            return scores;
        }

        private Node ToBST(IList<DocumentPosting> sorted, int start, int end)
        {
            if (start > end) return null;

            int mid = (start + end) / 2;
            Node node = new Node(sorted[mid]);

            node.Left = ToBST(sorted, start, mid - 1);
            node.Right = ToBST(sorted, mid + 1, end);

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

            public IEnumerable<Node> All()
            {
                var stack = new Stack<Node>();
                var node = this;

                while (node != null)
                {
                    yield return node;

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
            }

            public bool TryGetSubTree(int documentId, out Node subTree)
            {
                var node = this;
                var stack = new Stack<Node>();

                while (node != null)
                {
                    if (node.Data.DocumentId == documentId)
                    {
                        subTree = node;
                        return true;
                    }

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

                subTree = null;
                return false;
            }

            public float FindNearPostings(
                DocumentPosting posting, int maxDistance)
            {
                float score = 0;
                var node = this;
                //var debugList = new List<DocumentPosting>();
                var stack = new Stack<Node>();

                while (node!= null)
                {
                    if (posting.DocumentId != node.Data.DocumentId)
                    {
                        break;
                    }

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

                    score += (float)1 / distance;
                    //debugList.Add(node.Data);

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
