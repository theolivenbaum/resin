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
    /// Binary tree where the data is a sparse vector, a word embedding.
    /// The tree is balanced according to cos angles between the word vectors of the immediate neighbouring nodes.
    /// </summary>
    public class VectorNode
    {
        public const float IdenticalAngle = 0.97f;
        public const float FoldAngle = 0.55f;

        private VectorNode _right;
        private VectorNode _left;
        private ConcurrentBag<ulong> _docIds;

        public long VecOffset { get; private set; }
        public long PostingsOffset { get; set; }
        public float Angle { get; private set; }
        public SortedList<int, byte> TermVector { get; }
        public VectorNode Ancestor { get; private set; }

        public VectorNode Right
        {
            get => _right;
            set
            {
                _right = value;
                _right.Ancestor = this;
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                _left = value;
                _left.Ancestor = this;
            }
        }

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

        public VectorNode Clone()
        {
            var clone = new VectorNode(TermVector);

            clone.VecOffset = VecOffset;
            clone.PostingsOffset = PostingsOffset;
            clone._docIds = new ConcurrentBag<ulong>(_docIds);
            clone.Angle = Angle;
            clone.Ancestor = Ancestor;

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

        public async Task Add(VectorNode node, Stream vectorStream = null)
        {
            var angle = node.TermVector.CosAngle(TermVector);

            if (angle >= IdenticalAngle)
            {
                node.Angle = angle;

                Merge(node);
            }
            else if (angle > FoldAngle)
            {
                if (Left == null)
                {
                    node.Angle = angle;
                    Left = node;

                    if (vectorStream != null)
                        await Left.SerializeVector(vectorStream);
                }
                else
                {
                    await Left.Add(node, vectorStream);
                }
            }
            else
            {
                if (Right == null)
                {
                    node.Angle = angle;
                    Right = node;

                    if (vectorStream != null)
                        await Right.SerializeVector(vectorStream);
                }
                else
                {
                    await Right.Add(node, vectorStream);
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
            if (VecOffset < 0)
            {
                throw new InvalidOperationException();
            }

            if (PostingsOffset < 0)
            {
                throw new InvalidOperationException();
            }

            var block = new byte[5][];

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
            block[4] = terminator;

            return block;
        }

        public async Task<(long offset, long length)> SerializeTree(Stream indexStream)
        {
            var node = Right;
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

        private async Task SerializeVector(Stream vectorStream)
        {
            VecOffset = await TermVector.SerializeAsync(vectorStream);
        }

        public IEnumerable<VectorNode> SerializePostings(Stream lengths, Stream lists)
        {
            var node = Right;
            var stack = new Stack<VectorNode>();

            while (node != null)
            {
                if (node._docIds.Count > 0)
                {
                    // dirty node

                    var list = node._docIds.Distinct().ToArray();

                    node._docIds.Clear();

                    var buf = list.ToStream();

                    if (buf.Length / sizeof(ulong) != list.Length)
                    {
                        throw new DataMisalignedException();
                    }

                    lists.Write(buf);
                    lengths.Write(BitConverter.GetBytes(buf.Length));

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

        public const int NodeSize = sizeof(float) + sizeof(long) + sizeof(long) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(int) + sizeof(byte);

        public static async Task<VectorNode> Deserialize(Stream indexStream, Stream vectorStream, long indexOffset, long indexLength)
        {
            var buf = new byte[NodeSize];
            int read = 0;
            VectorNode root = new VectorNode();
            VectorNode cursor = root;
            var tail = new Stack<VectorNode>();
            Byte terminator = 2;

            if (indexStream.Position != indexOffset)
            {
                indexStream.Seek(indexOffset, SeekOrigin.Begin);
            }

            while (read < indexLength)
            {
                read += await indexStream.ReadAsync(buf, 0, NodeSize);

                // Deserialize node
                var angle = BitConverter.ToSingle(buf, 0);
                var vecOffset = BitConverter.ToInt64(buf, sizeof(float));
                var postingsOffset = BitConverter.ToInt64(buf, sizeof(float) + sizeof(long));
                var vectorCount = BitConverter.ToInt32(buf, sizeof(float) + sizeof(long) + sizeof(long));

                // Deserialize term vector
                var vec = new SortedList<int, byte>();
                var vecBuf = new byte[vectorCount * ComponentSize];

                vectorStream.Seek(vecOffset, SeekOrigin.Begin);

                await vectorStream.ReadAsync(vecBuf, 0, vecBuf.Length);

                var offs = 0;

                for (int i = 0; i < vectorCount; i++)
                {
                    var key = BitConverter.ToInt32(vecBuf, offs);
                    var val = vecBuf[offs + sizeof(int)];

                    vec.Add(key, val);

                    offs += ComponentSize;
                }

                // Create node
                var node = new VectorNode(vec);
                node.Angle = angle;
                node.PostingsOffset = postingsOffset;
                node.VecOffset = vecOffset;
                
                if (terminator == 0) // there is both a left and a right child
                {
                    cursor.Left = node;
                    tail.Push(cursor);
                }
                else if (terminator == 1) // there is a left but no right child
                {
                    cursor.Left = node;
                }
                else if (terminator == 2) // there is a right but no left child
                {
                    cursor.Right = node;
                }
                else // there are no children
                {
                    tail.Pop().Right = node;
                }

                cursor = node;
                terminator = buf[buf.Length - 1];
            }

            return root;
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

        public (int depth, int width) Size()
        {
            var root = this;
            var width = 0;
            var depth = 0;
            var node = root.Right;

            while (node != null)
            {
                var d = node.Depth();
                if (d > depth)
                {
                    depth = d;
                }
                width++;
                node = node.Right;
            }

            return (depth, width);
        }

        public override string ToString()
        {
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
