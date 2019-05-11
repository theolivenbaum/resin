using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sir.Store
{
    /// <summary>
    /// Binary tree that consists of nodes that carry vectors as their payload. 
    /// Nodes are balanced according to the cosine similarity of their vectors.
    /// </summary>
    public class VectorNode
    {
        public const int BlockSize = sizeof(long) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(long) + sizeof(int);

        private VectorNode _right;
        private VectorNode _left;
        private VectorNode _ancestor;
        private int _weight;
        private float _angleWhenAdded;

        public HashSet<long> DocIds { get; private set; }

        public int ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }
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

        public VectorNode()
            : this('\0'.ToString())
        {
        }

        public VectorNode(string s)
            : this(s.ToVector())
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

        public VectorNode(long postingsOffset, long vecOffset, byte terminator, int weight, int componentCount)
        {
            PostingsOffset = postingsOffset;
            VectorOffset = vecOffset;
            Terminator = terminator;
            Weight = weight;
            ComponentCount = componentCount;
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

        public Hit FindFirstNonSimilar(SortedList<long, int> vector, float foldAngle)
        {
            var cursor = this;

            while (cursor != null)
            {
                var angle = vector.CosAngle(cursor.Vector);

                if (angle < foldAngle)
                {
                    return new Hit
                    {
                        Score = angle,
                        Node = cursor
                    };

                }
                else if (cursor.Right != null)
                {
                    cursor = cursor.Right;
                }
                else
                {
                    cursor = cursor.Left;
                }
            }

            return new Hit();
        }

        public VectorNode Detach()
        {
            _ancestor = null;
            _left = null;
            _right = null;
            _weight = 0;

            return this;
        }

        public void DetachFromAncestor()
        {
            _ancestor = null;
        }

        public void Add(VectorNode node, (float identicalAngle, float foldAngle) similarity)
        {
            var cursor = this;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                cursor._angleWhenAdded = angle;

                if (angle >= similarity.identicalAngle)
                {
                    lock (this)
                    {
                        cursor.Merge(node);
                    }

                    break;
                }
                else if (angle > similarity.foldAngle)
                {
                    if (cursor.Left == null)
                    {
                        lock (this)
                        {
                            if (cursor.Left == null)
                            {
                                cursor.Left = node;

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
                        lock (this)
                        {
                            if (cursor.Right == null)
                            {
                                cursor.Right = node;

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
        }

        public void Merge(VectorNode node)
        {
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

        public void Serialize(Stream stream)
        {
            byte terminator = 1;

            if (Left == null && Right == null) // there are no children
            {
                terminator = 3;
            }
            else if (Left == null) // there is a right but no left
            {
                terminator = 2;
            }
            else if (Right == null) // there is a left but no right
            {
                terminator = 1;
            }
            else // there is a left and a right
            {
                terminator = 0;
            }

            stream.Write(BitConverter.GetBytes(VectorOffset));
            stream.Write(BitConverter.GetBytes(PostingsOffset));
            stream.Write(BitConverter.GetBytes(Vector.Count));
            stream.Write(BitConverter.GetBytes(Weight));
            stream.WriteByte(terminator);
        }

        public (long offset, long length) SerializeTree(Stream indexStream, Stream vectorStream)
        {
            var node = this;
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;

            while (node != null)
            {
                node.SerializeVector(vectorStream);

                node.Serialize(indexStream);

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

        public IEnumerable<VectorNode> All()
        {
            var node = this;
            var stack = new Stack<VectorNode>();

            while (node != null)
            {
                if (node._ancestor != null)
                {
                    yield return node;
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

        public SortedList<long, int> Compress()
        {
            var vector = new SortedList<long, int>();

            foreach(var node in All())
            {
                vector = VectorOperations.Merge(vector, node.Vector);
            }

            return vector;
        }

        public string Visualize()
        {
            StringBuilder output = new StringBuilder();
            Visualize(this, output, 0);
            return output.ToString();
        }

        private void Visualize(VectorNode node, StringBuilder output, int depth)
        {
            if (node == null) return;

            output.Append('\t', depth);
            output.AppendFormat($"{node._angleWhenAdded} {node} w:{node.Weight}");
            output.AppendLine();

            Visualize(node.Left, output, depth + 1);
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
                w.Append(c.Key);
                w.Append('.');
            }

            return w.ToString();
        }
    }

    public static class StreamHelper
    {
        public static byte[] ToStream(this IEnumerable<long> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                var buf = BitConverter.GetBytes(item);

                payload.Write(buf, 0, buf.Length);
            }

            return payload.ToArray();
        }

        public static byte[] ToStream(this IEnumerable<int> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                var buf = BitConverter.GetBytes(item);

                payload.Write(buf, 0, buf.Length);
            }

            return payload.ToArray();
        }

        public static byte[] ToStream(this IEnumerable<KeyValuePair<long, int>> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                payload.Write(BitConverter.GetBytes(item.Key));
                payload.Write(BitConverter.GetBytes(item.Value));
            }

            return payload.ToArray();
        }
    }
}
