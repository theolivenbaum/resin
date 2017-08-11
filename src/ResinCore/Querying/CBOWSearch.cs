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
            var phraseQuery = (PhraseQuery)ctx.Query;
            var tokens = phraseQuery.Values;
            var terms = new List<Term>(tokens.Count);
            var postingsCache = new Dictionary<string, IList<DocumentPosting>>();

            for (int index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];

                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    var words = reader.IsWord(token);
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

            var weights = new DocumentScore[postings[0].Count][];

            SetWeights(postings, weights);

           var timer = Stopwatch.StartNew();

            var scores = new Dictionary<int, DocumentScore>();

            foreach(DocumentScore[] score in weights)
            {
                if (score != null)
                {
                    DocumentScore sum = score[0];
                    for (int i = 1; i < score.Length; i++)
                    {
                        var s = score[i];
                        if (s == null)
                        {
                            sum = null;
                            break;
                        }
                        sum.Add(s);
                    }
                    if (sum != null)
                    {
                        DocumentScore existing;
                        if (scores.TryGetValue(sum.DocumentId, out existing))
                        {
                            if (sum.Score > existing.Score)
                            {
                                scores[sum.DocumentId] = sum;
                            }
                        }
                        else
                        {
                            scores[sum.DocumentId] = sum;
                        }
                    }
                }
            }

            Log.DebugFormat("scored weights in {0}", timer.Elapsed);

            return scores.Values.ToList();
        }

        private void SetWeights(IList<IList<DocumentPosting>> postings, DocumentScore[][] weights)
        {
            int maxDistance = postings.Count;
            var timer = Stopwatch.StartNew();
            var first = postings[0];

            for (int index = 1; index < postings.Count; index++)
            {
                var second = postings[index];

                Log.DebugFormat(
                    "measuring distances in documents between word {0} and word {1}", index - 1, index);

                Score(weights, ref first, second, ++maxDistance, postings.Count - 1, index - 1);
            }

            Log.DebugFormat("produced {0} weights in {1}",
                    weights.Length, timer.Elapsed);
        }

        private void Score (
            DocumentScore[][] weights, ref IList<DocumentPosting> list1, 
            IList<DocumentPosting> list2, int maxDistance, int numOfPasses, int passIndex)
        {
            var cursor1 = 0;
            var cursor2 = 0;

            while (cursor1 < list1.Count && cursor2 < list2.Count)
            {
                if (list1[cursor1] == null)
                {
                    cursor1++;
                    continue;
                }

                var p1 = list1[cursor1];
                var p2 = list2[cursor2];

                if (p2.DocumentId > p1.DocumentId)
                {
                    list1[cursor1] = null;
                    cursor1++;
                    continue;
                }
                else if (p1.DocumentId > p2.DocumentId)
                {
                    cursor2++;
                    continue;
                }

                var distance = p2.Position - p1.Position;

                if (distance < 0)
                {
                    cursor2++;
                    continue;
                }

                if (distance <= maxDistance)
                {
                    var score = (double)1 / Math.Max(1, distance);
                    var documentScore = new DocumentScore(p1.DocumentId, score, Session.Version);

                    if (weights[cursor1] == null)
                    {
                        weights[cursor1] = new DocumentScore[numOfPasses];
                        weights[cursor1][passIndex] = documentScore;
                    }
                    else
                    {
                        if (weights[cursor1][passIndex] == null || 
                            weights[cursor1][passIndex].Score < score)
                        {
                            weights[cursor1][passIndex] = documentScore;
                        }
                    }

                    //Log.DebugFormat("document ID {0} scored {1}",
                    //    p1.DocumentId, score);
                }
                else
                {
                    list1[cursor1] = null;
                }
                cursor1++;
            }
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
