using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Binary tree where the data is a vector and that is
    /// balanced according to the cos angles between the vectors of the immediate neighbouring nodes.
    /// </summary>
    public class VectorNode
    {
        public const int NodeSize = sizeof(float) + sizeof(long) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(int) + sizeof(byte);
        public const float TermIdenticalAngle = 0.97f;
        public const float TermFoldAngle = 0.65f;
        public const float DocIdenticalAngle = 0.97f;
        public const float DocFoldAngle = 0.65f;

        private VectorNode _right;
        private VectorNode _left;
        private HashSet<long> _docIds;
        private int _weight;

        public long VectorOffset { get; private set; }
        public long PostingsOffset { get; set; }
        public float Angle { get; private set; }
        public SortedList<int, byte> Vector { get; }
        public VectorNode Ancestor { get; private set; }
        public int Weight
        {
            get { return _weight; }
            set
            {
                var diff = value - _weight;

                _weight = value;

                if (Ancestor != null)
                {
                    Ancestor.Weight += diff;
                }
            }
        }

        public VectorNode Right
        {
            get => _right;
            set
            {
                _right = value;
                _right.Ancestor = this;
                Weight++;
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                _left = value;
                _left.Ancestor = this;
                Weight++;
            }
        }

        public byte Terminator { get; set; }

        public VectorNode()
            : this('\0'.ToString())
        {
        }

        public VectorNode(string s)
            : this(s.ToCharVector())
        {
        }

        public VectorNode(SortedList<int, byte> termVector)
        {
            Vector = termVector;
            PostingsOffset = -1;
            VectorOffset = -1;
        }

        public VectorNode(SortedList<int, byte> termVector, long docId)
        {
            Vector = termVector;
            PostingsOffset = -1;
            VectorOffset = -1;
            _docIds = new HashSet<long>();
            _docIds.Add(docId);
        }

        public Hit ClosestMatch(VectorNode node, float foldAngle)
        {
            var best = this;
            var cursor = this;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

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

            return new Hit { Embedding = best.Vector, Score = highscore, PostingsOffset = best.PostingsOffset };
        }

        private readonly object _sync = new object();

        public void Add(VectorNode node, float identicalAngle, float foldAngle, Stream vectorStream = null)
        {
            node.Ancestor = null;
            node._left = null;
            node._right = null;
            node._weight = 0;

            var cursor = this;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                if (angle >= identicalAngle)
                {
                    node.Angle = angle;

                    lock (_sync)
                    {
                        cursor.Merge(node);
                    }

                    break;
                }
                else if (angle > foldAngle)
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

        public void Merge(IEnumerable<long> docIds)
        {
            if (_docIds == null)
            {
                _docIds = new HashSet<long>();
            }

            foreach (var id in docIds)
            {
                _docIds.Add(id);
            }
        }

        public void Merge(VectorNode node)
        {
            if (_docIds == null)
            {
                _docIds = node._docIds;
            }
            else if (node._docIds != null)
            {
                foreach (var id in node._docIds)
                {
                    _docIds.Add(id);
                }
            }
        }

        private byte[][] ToStream()
        {
            if (Ancestor != null)
            {
                if (VectorOffset < 0)
                {
                    throw new InvalidOperationException();
                }

                if (PostingsOffset < 0)
                {
                    throw new InvalidOperationException();
                }
            }

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
                foreach (var buf in node.ToStream())
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
                if (node._docIds != null)
                {
                    // dirty node

                    var list = node._docIds.ToArray();

                    node._docIds.Clear();

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

        public static VectorNode DeserializeTree(Stream indexStream, Stream vectorStream, long indexLength)
        {
            VectorNode root = new VectorNode();
            VectorNode cursor = root;
            var tail = new Stack<VectorNode>();
            byte terminator = 2;
            int read = 0;
            var buf = new byte[NodeSize];

            while (read < indexLength)
            {
                indexStream.Read(buf);

                var node = DeserializeNode(buf, vectorStream, ref terminator);

                if (node.Terminator == 0) // there is both a left and a right child
                {
                    cursor.Left = node;
                    tail.Push(cursor);
                }
                else if (node.Terminator == 1) // there is a left but no right child
                {
                    cursor.Left = node;
                }
                else if (node.Terminator == 2) // there is a right but no left child
                {
                    cursor.Right = node;
                }
                else // there are no children
                {
                    if (tail.Count > 0)
                    {
                        tail.Pop().Right = node;
                    }
                }

                cursor = node;
                read += NodeSize;
            }

            root.Right.Ancestor = null;

            return root.Right;
        }

        public static VectorNode DeserializeNode(byte[] buf, Stream vectorStream, ref byte terminator)
        {
            // Deserialize node
            var angle = BitConverter.ToSingle(buf, 0);
            var vecOffset = BitConverter.ToInt64(buf, sizeof(float));
            var postingsOffset = BitConverter.ToInt64(buf, sizeof(float) + sizeof(long));
            var vectorCount = BitConverter.ToInt32(buf, sizeof(float) + sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt32(buf, sizeof(float) + sizeof(long) + sizeof(long) + sizeof(int));

            // Deserialize term vector
            var vec = new SortedList<int, byte>();
            var vecBuf = new byte[vectorCount * ComponentSize];

            if (vecOffset < 0)
            {
                vec.Add(0, 1);
            }
            else
            {
                vectorStream.Seek(vecOffset, SeekOrigin.Begin);
                vectorStream.Read(vecBuf, 0, vecBuf.Length);

                var offs = 0;

                for (int i = 0; i < vectorCount; i++)
                {
                    var key = BitConverter.ToInt32(vecBuf, offs);
                    var val = vecBuf[offs + sizeof(int)];

                    vec.Add(key, val);

                    offs += ComponentSize;
                }
            }

            // Create node
            var node = new VectorNode(vec);

            node.Angle = angle;
            node.PostingsOffset = postingsOffset;
            node.VectorOffset = vecOffset;
            node.Terminator = terminator;
            node.Weight = weight;

            terminator = buf[buf.Length - 1];

            return node;
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
                if (cursor.Ancestor == null) break;
                cursor = cursor.Ancestor;
            }
            return cursor;
        }

        public IEnumerable<VectorNode> All()
        {
            var node = this;
            var stack = new Stack<VectorNode>();

            while (node != null)
            {
                yield return node;

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

            if (node.Ancestor != null)
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
            var root = this;
            var width = 0;
            var depth = 1;
            var node = root.Right;
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
            if (Vector == null) return string.Empty;

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
