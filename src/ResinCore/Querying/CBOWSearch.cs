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
            : base(session, scoringFactory, postingsReader)
        {
        }

        public void Search(QueryContext ctx)
        {
            var tokens = ((PhraseQuery)ctx.Query).Values;
            var terms = new List<Term>(tokens.Count);
            var timer = Stopwatch.StartNew();

            for (int index = 0; index < tokens.Count; index++)
            {
                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    var words = reader.IsWord(tokens[index]);
                    if (words.Count > 0)
                    {
                        terms.Add(new Term(ctx.Query.Field, words[0]));
                    }
                }
            }

            var postings = terms.Count > 0 ? GetManyPostingsLists(terms): null;

            Log.DebugFormat("read postings for {0} in {1}", ctx.Query, timer.Elapsed);

            ctx.Scores = Score(postings);
        }

        private IList<DocumentScore> Score(IList<IList<DocumentPosting>> postings)
        {
            var trees = new Node[postings.Count];

            for (int i = 0; i<postings.Count; i++)
            {
                var timer = Stopwatch.StartNew();

                trees[i] = ToBST(postings[i], 0, postings[i].Count-1);

                Log.DebugFormat("built postings tree with len {0} in {1}", 
                    postings[i].Count, timer.Elapsed);
            }

            if (postings.Count == 1)
            {
                return Score(postings[0]);
            }

            var weights = new List<DocumentScore>[postings.Count - 1];

            SetWeights(postings, trees, weights);

            return weights.Sum();
        }

        private void SetWeights(IList<IList<DocumentPosting>> postings, Node[] trees, IList<DocumentScore>[] weights)
        {
            Log.Debug("measuring distances in documents between first and second word");

            var timer = Stopwatch.StartNew();

            var first = postings[0];
            var maxDistance = trees.Length;
            var firstScoreList = Score(first, trees[1], maxDistance);

            weights[0] = firstScoreList;

            Log.DebugFormat("produced {0} scores in {1}",
                    firstScoreList.Count, timer.Elapsed);

            for (int index = 2; index < trees.Length; index++)
            {
                timer = Stopwatch.StartNew();

                maxDistance++;

                Log.DebugFormat(
                    "measuring distances in documents between first word and word {0}", index);

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
                    }

                    if (distance > maxDistance)
                    {
                        if(node.Data.Position < posting.Position)
                        {
                            node = node.Right;
                            continue;
                        }
                        else
                        {
                            node = node.Left;
                            continue;
                        }
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
