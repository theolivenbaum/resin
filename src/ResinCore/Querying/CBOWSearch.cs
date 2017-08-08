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
            var phraseQuery = (PhraseQuery)ctx.Query;
            var tokens = phraseQuery.Values;
            var terms = new List<Term>(tokens.Count);

            Log.DebugFormat("executing {0}", phraseQuery);

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

            ctx.Scores = Score(postings);
        }

        private IList<DocumentScore> Score(IList<IList<DocumentPosting>> postings)
        {
            if (postings.Count == 1)
            {
                return Score(postings[0]);
            }

            var weights = new List<DocumentScore>[postings.Count - 1];

            SetWeights(postings, weights);

            return weights.Sum();
        }

        private void SetWeights(IList<IList<DocumentPosting>> postings, IList<DocumentScore>[] weights)
        {
            Log.Debug("measuring distances in documents between word 0 and word 1");

            var timer = Stopwatch.StartNew();

            var first = postings[0];
            var maxDistance = postings.Count;
            var firstScoreList = Score(first, postings[1], maxDistance);

            weights[0] = firstScoreList;

            Log.DebugFormat("produced {0} scores in {1}",
                    firstScoreList.Count, timer.Elapsed);

            for (int index = 2; index < postings.Count; index++)
            {
                timer = Stopwatch.StartNew();

                maxDistance++;

                Log.DebugFormat(
                    "measuring distances in documents between word 0 and word {0}", index);

                var scores = Score(first, postings[index], maxDistance);

                weights[index-1] = scores;

                Log.DebugFormat("produced {0} scores in {1}",
                    scores.Count, timer.Elapsed);
            }
        }

        private IList<DocumentScore> Score (
            IList<DocumentPosting> list1,
            IList<DocumentPosting> list2, 
            int maxDistance)
        {
            var scores = new List<DocumentScore>();
            var cursor1 = 0;
            var cursor2 = 0;

            while (cursor1 < list1.Count && cursor2 < list2.Count)
            {
                var p1 = list1[cursor1];
                var p2 = list2[cursor2];

                while (p1.DocumentId > p2.DocumentId)
                {
                    if (cursor2 == list2.Count) break;

                    p2 = list2[cursor2++];
                }

                if (p1.DocumentId > p2.DocumentId)
                {
                    break;
                }

                while (p1.DocumentId < p2.DocumentId)
                {
                    if (cursor1 == list1.Count) break;

                    p1 = list1[cursor1++];
                }

                if (p1.DocumentId < p2.DocumentId)
                {
                    break;
                }

                var distance = p2.Position - p1.Position;

                if (distance < 0)
                {
                    distance = Math.Abs(distance)+1;
                    if (distance > maxDistance)
                    {
                        var documentId = p1.DocumentId;
                        while (documentId == p1.DocumentId)
                        {
                            if (cursor1 == list1.Count) break;

                            p1 = list1[cursor1++];
                        }
                    }
                    else
                    {
                        cursor1++;
                    }

                }
                else
                {
                    cursor1++;
                }

                if (distance <= maxDistance)
                {
                    var score = (float)1 / distance;

                    scores.Add(new DocumentScore(p1.DocumentId, score, Session.Version));

                    Log.DebugFormat("document ID {0} scored {1}",
                        p1.DocumentId, score);
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
