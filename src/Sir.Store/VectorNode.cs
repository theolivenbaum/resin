using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Binary tree where the data is a word embedding represented in-memory as a sparse vector.
    /// The tree is balanced according to cos angles between the word vectors of the immediate neighbouring nodes.
    /// </summary>
    public class VectorNode
    {
        public const int NodeSize = sizeof(float) + sizeof(long) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(int) + sizeof(byte);
        public const float IdenticalAngle = 0.97f;
        public const float FoldAngle = 0.65f;

        private VectorNode _right;
        private VectorNode _left;
        private ConcurrentBag<ulong> _docIds;
        private int _weight;

        public long VecOffset { get; private set; }
        public long PostingsOffset { get; set; }
        public float Angle { get; private set; }
        public SortedList<int, byte> TermVector { get; }
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
            _docIds = new ConcurrentBag<ulong>();
            TermVector = termVector;
            PostingsOffset = -1;
            VecOffset = -1;
        }

        public VectorNode(string s, ulong docId)
        {
            if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException();

            _docIds = new ConcurrentBag<ulong> { docId };
            TermVector = s.ToCharVector();
            PostingsOffset = -1;
            VecOffset = -1;
        }

        public VectorNode(byte[] buffer)
        {

        }

        public VectorNode Clone()
        {
            var clone = new VectorNode(TermVector);

            clone.VecOffset = VecOffset;
            clone.PostingsOffset = PostingsOffset;
            clone._docIds = new ConcurrentBag<ulong>(_docIds);
            clone.Angle = Angle;
            clone.Ancestor = Ancestor;
            clone.Terminator = Terminator;

            clone._left = _left == null ? null : _left.Clone();
            clone._right = _right == null ? null : _right.Clone();

            return clone;
        }

        public Hit ClosestMatch(VectorNode node)
        {
            var best = this;
            var cursor = this;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = node.TermVector.CosAngle(cursor.TermVector);

                if (angle > FoldAngle)
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

            return new Hit { Embedding = best.TermVector, Score = highscore, PostingsOffset = best.PostingsOffset };
        }

        private readonly object _sync = new object();

        public void Add(VectorNode node, Stream vectorStream = null)
        {
            var cursor = this;

            while (cursor != null)
            {
                var angle = node.TermVector.CosAngle(cursor.TermVector);

                if (angle >= IdenticalAngle)
                {
                    node.Angle = angle;

                    cursor.Merge(node);

                    break;
                }
                else if (angle > FoldAngle)
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
                else
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
        }

        private void Merge(VectorNode node)
        {
            if (VecOffset < 0)
            {
                throw new InvalidOperationException();
            }

            foreach (var id in node._docIds)
            {
                _docIds.Add(id);
            }
        }

        private byte[][] ToStream()
        {
            if (Ancestor != null)
            {
                if (VecOffset < 0)
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
            block[1] = BitConverter.GetBytes(VecOffset);
            block[2] = BitConverter.GetBytes(PostingsOffset);
            block[3] = BitConverter.GetBytes(TermVector.Count);
            block[4] = BitConverter.GetBytes(Weight);
            block[5] = terminator;

            return block;
        }

        public async Task<(long offset, long length)> SerializeTree(Stream indexStream)
        {
            var node = this;
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;

            while (node != null)
            {
                foreach (var buf in node.ToStream())
                {
                    await indexStream.WriteAsync(buf, 0, buf.Length);
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

        private void SerializeVector(Stream vectorStream)
        {
            VecOffset = TermVector.Serialize(vectorStream);
        }

        private async Task SerializeVectorAsync(Stream vectorStream)
        {
            VecOffset = await TermVector.SerializeAsync(vectorStream);
        }

        public IList<VectorNode> SerializePostings(Stream lengths, Stream offsets, Stream lists)
        {
            var node = this;
            var stack = new Stack<VectorNode>();
            var result = new List<VectorNode>();

            while (node != null)
            {
                if (node._docIds.Count > 0)
                {
                    // dirty node

                    var list = node._docIds.Distinct().ToArray();

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

        public static Hit ScanTree(VectorNode term, Stream indexStream, Stream vectorStream, long indexLength)
        {
            var buf = new byte[NodeSize];

            indexStream.Read(buf);

            byte terminator = buf[buf.Length - 1];
            int read = NodeSize;
            VectorNode cursor = DeserializeNode(buf, vectorStream, ref terminator);
            var tail = new Stack<VectorNode>();
            VectorNode best = cursor;
            var highscore = 0f;

            while (read < indexLength)
            {
                indexStream.Read(buf);

                var node = DeserializeNode(buf, vectorStream, ref terminator);

                var angle = node.TermVector.CosAngle(term.TermVector);

                if (angle > highscore)
                {
                    highscore = angle;
                    best = node;

                    if (angle >= IdenticalAngle)
                    {
                        break;
                    }
                }

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

            return new Hit { Embedding = best.TermVector, PostingsOffset = best.PostingsOffset, Score = highscore };
        }

        public static VectorNode DeserializeTree(Stream indexStream, Stream vectorStream, long indexLength)
        {
            //var buf = new byte[NodeSize];

            //indexStream.Read(buf);

            //byte terminator = buf[buf.Length - 1];
            //VectorNode root = DeserializeNode(buf, vectorStream, ref terminator);
            //VectorNode cursor = root;
            //var tail = new Stack<VectorNode>();
            //int read = 0;

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

            return root;
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
            node.VecOffset = vecOffset;
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

            return (depth, width, aggDepth/count);
        }

        public override string ToString()
        {
            if (TermVector == null) return string.Empty;

            var w = new StringBuilder();

            foreach (var c in TermVector)
            {
                w.Append((char)c.Key);
            }

            return w.ToString();
        }
    }

    public static class StreamHelper
    {
        public static byte[] ToStream(this IEnumerable<ulong> docIds)
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
