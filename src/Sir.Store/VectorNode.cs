using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Binary tree that consists of nodes that carry vectors as their payload. 
    /// Nodes are balanced according to the cosine similarity of their vectors.
    /// </summary>
    public class VectorNode
    {
        public const int BlockSize = sizeof(float) + sizeof(long) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(long) + sizeof(int);

        private VectorNode _right;
        private VectorNode _left;
        private VectorNode _ancestor;

        public HashSet<long> DocIds { get; private set; }
        private int _weight;

        public int ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }
        public float Angle { get; set; }
        public SortedList<long, int> Vector { get; set; }

        public int Weight
        {
            get { return _weight; }
            set
            {
                var diff = value - _weight;

                _weight = value;

                if (diff > 0)
                {
                    var cursor = _ancestor;
                    while (cursor != null)
                    {
                        cursor._weight += diff;
                        cursor = cursor._ancestor;
                    }
                }
            }
        }

        public VectorNode Right
        {
            get => _right;
            set
            {
                _right = value;
                _right._ancestor = this;
                Weight++;
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                _left = value;
                _left._ancestor = this;
                Weight++;
            }
        }

        public VectorNode Ancestor
        {
            get { return _ancestor; }
        }

        public byte Terminator { get; set; }

        public IList<long> PostingsOffsets { get; set; }

        public VectorNode(bool shallow)
        {
        }

        public VectorNode()
            : this('\0'.ToString())
        {
        }

        public VectorNode(string s)
            : this(s.ToCharVector())
        {
        }

        public VectorNode(SortedList<long, int> termVector)
        {
            Vector = termVector;
            PostingsOffset = -1;
            VectorOffset = -1;
        }

        public VectorNode(SortedList<long, int> vector, long docId)
        {
            Vector = vector;
            PostingsOffset = -1;
            VectorOffset = -1;
            DocIds = new HashSet<long>();
            DocIds.Add(docId);
        }

        public void DetachFromParent()
        {
            _ancestor = null;
        }

        public Hit ClosestMatch(SortedList<long, int> vector, float foldAngle)
        {
            var best = this;
            var cursor = this;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = vector.CosAngle(cursor.Vector);

                if (angle > foldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }

                    cursor = cursor.Left;
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    cursor = cursor.Right;
                }
            }

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        public IEnumerable<Hit> Intersecting(VectorNode node, float foldAngle)
        {
            var intersecting = new List<Hit>();
            var cursor = this;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                if (angle > 0)
                {
                    intersecting.Add(new Hit
                    {
                        Score = angle,
                        Node = cursor
                    });
                }

                if (angle > foldAngle)
                {
                    cursor = cursor.Left;
                }
                else
                {
                    cursor = cursor.Right;
                }
            }

            return intersecting.OrderByDescending(x => x.Score);
        }

        private readonly object _sync = new object();

        public VectorNode Add(
            VectorNode node, 
            (float identicalAngle, float foldAngle) similarity, 
            Stream vectorStream = null,
            bool vectorAddition = false)
        {
            node._ancestor = null;
            node._left = null;
            node._right = null;
            node._weight = 0;

            var cursor = this;
            var junction = this;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                if (angle >= similarity.identicalAngle)
                {
                    node.Angle = angle;

                    lock (_sync)
                    {
                        cursor.Merge(node, vectorAddition, vectorStream);
                    }

                    junction = cursor;
                    break;
                }
                else if (angle > similarity.foldAngle)
                {
                    if (cursor.Left == null)
                    {
                        lock (_sync)
                        {
                            if (cursor.Left == null)
                            {
                                node.Angle = angle;
                                cursor.Left = node;

                                if (vectorStream != null)
                                    cursor.Left.SerializeVector(vectorStream);

                                junction = node;
                                break;
                            }
                            else
                            {
                                cursor = cursor.Left;
                            }
                        }
                    }
                    else
                    {
                        cursor = cursor.Left;
                    }
                }
                else
                {
                    if (cursor.Right == null)
                    {
                        lock (_sync)
                        {
                            if (cursor.Right == null)
                            {
                                node.Angle = angle;
                                cursor.Right = node;

                                if (vectorStream != null)
                                    cursor.Right.SerializeVector(vectorStream);

                                junction = node;
                                break;
                            }
                            else
                            {
                                cursor = cursor.Right;
                            }
                        }
                    }
                    else
                    {
                        cursor = cursor.Right;
                    }
                }
            }

            return junction;
        }

        public void Merge(VectorNode node, bool vectorAddition, Stream vectorStream = null)
        {
            if (vectorAddition)
            {
                Vector = Vector.Add(node.Vector);

                SerializeVector(vectorStream);
            }

            if (DocIds == null)
            {
                DocIds = node.DocIds;
            }
            else
            {
                foreach (var id in node.DocIds)
                {
                    DocIds.Add(id);
                }
            }

            if (node.PostingsOffset >= 0)
            {
                if (PostingsOffset >= 0)
                {
                    if (PostingsOffsets == null)    
                    {
                        PostingsOffsets = new List<long> { PostingsOffset, node.PostingsOffset };
                    }
                    else
                    {
                        PostingsOffsets.Add(node.PostingsOffset);
                    }
                }
                else
                {
                    PostingsOffset = node.PostingsOffset;
                }
            }
        }

        public async Task Serialize(Stream stream)
        {
            foreach (var buf in ToStreams())
            {
                await stream.WriteAsync(buf, 0, buf.Length);
            }
        }

        public byte[][] ToStreams()
        {
            var block = new byte[6][];

            byte[] terminator = new byte[1];

            if (Left == null && Right == null) // there are no children
            {
                terminator[0] = 3;
            }
            else if (Left == null) // there is a right but no left
            {
                terminator[0] = 2;
            }
            else if (Right == null) // there is a left but no right
            {
                terminator[0] = 1;
            }
            else // there is a left and a right
            {
                terminator[0] = 0;
            }

            block[0] = BitConverter.GetBytes(Angle);
            block[1] = BitConverter.GetBytes(VectorOffset);
            block[2] = BitConverter.GetBytes(PostingsOffset);
            block[3] = BitConverter.GetBytes(Vector.Count);
            block[4] = BitConverter.GetBytes(Weight);
            block[5] = terminator;

            return block;
        }

        public (long offset, long length) SerializeTree(Stream indexStream)
        {
            var node = this;
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;

            while (node != null)
            {
                foreach (var buf in node.ToStreams())
                {
                    indexStream.Write(buf, 0, buf.Length);
                }

                if (node.Right != null)
                {
                    stack.Push(node.Right);
                }

                node = node.Left;

                if (node == null && stack.Count > 0)
                {
                    node = stack.Pop();
                }
            }

            var length = indexStream.Position - offset;

            return (offset, length);
        }

        public void SerializeVector(Stream vectorStream)
        {
            VectorOffset = Vector.Serialize(vectorStream);
        }

        private async Task SerializeVectorAsync(Stream vectorStream)
        {
            VectorOffset = await Vector.SerializeAsync(vectorStream);
        }

        public IList<VectorNode> SerializePostings(Stream lengths, Stream offsets, Stream lists)
        {
            var node = this;
            var stack = new Stack<VectorNode>();
            var result = new List<VectorNode>();

            while (node != null)
            {
                if (node.DocIds != null)
                {
                    // dirty node

                    var list = node.DocIds.ToArray();

                    node.DocIds.Clear();

                    var buf = list.ToStream();

                    lists.Write(buf);
                    lengths.Write(BitConverter.GetBytes(buf.Length));
                    offsets.Write(BitConverter.GetBytes(node.PostingsOffset));

                    result.Add(node);
                }

                if (node.Right != null)
                {
                    stack.Push(node.Right);
                }

                node = node.Left;

                if (node == null)
                {
                    if (stack.Count > 0)
                        node = stack.Pop();
                }
            }

            return result;
        }


        public string Visualize()
        {
            StringBuilder output = new StringBuilder();
            Visualize(this, output, 0);
            return output.ToString();
        }

        public int Depth()
        {
            var count = 0;
            var node = Left;

            while (node != null)
            {
                count++;
                node = node.Left;
            }
            return count;
        }

        public VectorNode GetRoot()
        {
            var cursor = this;
            while (cursor != null)
            {
                if (cursor._ancestor == null) break;
                cursor = cursor._ancestor;
            }
            return cursor;
        }

        public VectorNode ShallowCopy()
        {
            return new VectorNode (Vector)
            {
                VectorOffset = VectorOffset,
                PostingsOffset = PostingsOffset,
                PostingsOffsets = PostingsOffsets
            };
        }

        public IEnumerable<VectorNode> All()
        {
            var node = this;
            var stack = new Stack<VectorNode>();

            while (node != null)
            {
                if (node.PostingsOffset > -1)
                {
                    yield return node.ShallowCopy();
                }

                if (node.Right != null)
                {
                    stack.Push(node.Right);
                }

                node = node.Left;

                if (node == null)
                {
                    if (stack.Count > 0)
                        node = stack.Pop();
                }
            }
        }

        private void Visualize(VectorNode node, StringBuilder output, int depth)
        {
            if (node == null) return;

            float angle = 0;

            if (node._ancestor != null)
            {
                angle = node.Angle;
            }

            output.Append('\t', depth);
            output.AppendFormat(".{0} ({1})", node.ToString(), angle);
            output.AppendLine();

            if (node.Left != null)
                Visualize(node.Left, output, depth + 1);

            if (node.Right != null)
                Visualize(node.Right, output, depth);
        }

        public (int depth, int width, int avgDepth) Size()
        {
            var width = 0;
            var depth = 1;
            var node = this;
            var aggDepth = 0;
            var count = 0;

            while (node != null)
            {
                var d = node.Depth();
                if (d > depth)
                {
                    depth = d;
                }

                aggDepth += d;
                count++;
                width++;

                node = node.Right;
            }

            return (depth, width, aggDepth / count);
        }

        public override string ToString()
        {
            var w = new StringBuilder();

            foreach (var c in Vector)
            {
                w.Append((char)c.Key);
            }

            return w.ToString();
        }
    }

    public static class StreamHelper
    {
        public static byte[] ToStream(this IEnumerable<long> docIds)
        {
            var payload = new MemoryStream();

            foreach (var id in docIds)
            {
                var buf = BitConverter.GetBytes(id);

                payload.Write(buf, 0, buf.Length);
            }

            return payload.ToArray();
        }
    }
}
