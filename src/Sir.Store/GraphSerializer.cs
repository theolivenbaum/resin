using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public static class GraphSerializer
    {
        public static bool Add(VectorNode root, VectorNode node, (float identicalAngle, float foldAngle) similarity)
        {
            var cursor = root;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                if (angle >= similarity.identicalAngle)
                {
                    lock (cursor.Sync)
                    {
                        Merge(cursor, node);

                        return false;
                    }
                }
                else if (angle > similarity.foldAngle)
                {
                    if (cursor.Left == null)
                    {
                        lock (cursor.Sync)
                        {
                            if (cursor.Left == null)
                            {
                                node.AngleWhenAdded = angle;
                                cursor.Left = node;

                                return true;
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
                        lock (cursor.Sync)
                        {
                            if (cursor.Right == null)
                            {
                                node.AngleWhenAdded = angle;
                                cursor.Right = node;

                                return true;
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

            return false;
        }

        public static void MergePostings(VectorNode target, VectorNode node)
        {
            if (target.PostingsOffsets == null)
            {
                target.PostingsOffsets = new List<long> { target.PostingsOffset, node.PostingsOffset };
            }
            else
            {
                target.PostingsOffsets.Add(node.PostingsOffset);
            }
        }

        public static void Merge(VectorNode target, VectorNode node)
        {
            MergeDocIds(target, node);
        }

        public static void MergeDocIds(VectorNode target, VectorNode node)
        {
            foreach (var docId in node.DocIds)
            {
                target.DocIds.Add(docId);
            }
        }

        public static SortedList<long, int> Compress(VectorNode root)
        {
            var vector = new SortedList<long, int>();

            foreach (var node in PathFinder.All(root))
            {
                vector = vector.Add(node.Vector);
            }

            return vector;
        }

        public static void SerializeNode(VectorNode node, Stream stream)
        {
            byte terminator = 1;

            if (node.Left == null && node.Right == null) // there are no children
            {
                terminator = 3;
            }
            else if (node.Left == null) // there is a right but no left
            {
                terminator = 2;
            }
            else if (node.Right == null) // there is a left but no right
            {
                terminator = 1;
            }
            else // there is a left and a right
            {
                terminator = 0;
            }

            stream.Write(BitConverter.GetBytes(node.VectorOffset));
            stream.Write(BitConverter.GetBytes(node.PostingsOffset));
            stream.Write(BitConverter.GetBytes(node.Vector.Count));
            stream.Write(BitConverter.GetBytes(node.Weight));
            stream.WriteByte(terminator);
        }

        public static (long offset, long length) SerializeTree(VectorNode node, Stream indexStream, Stream vectorStream, Stream postingsStream)
        {
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;

            if (node.Vector.Count == 0)
            {
                node = node.Right;
            }

            while (node != null)
            {
                SerializePostings(node, postingsStream);
                SerializeVector(node, vectorStream);
                SerializeNode(node, indexStream);

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

        public static void SerializePostings(VectorNode node, Stream postingsStream)
        {
            var offset = postingsStream.Position;

            postingsStream.Write(node.DocIds.ToStreamWithHeader(node.DocIds.Count));

            node.PostingsOffset = offset;
        }

        public static void SerializeVector(VectorNode node, Stream vectorStream)
        {
            node.VectorOffset = node.Vector.Serialize(vectorStream);
        }

        public static long Serialize(this SortedList<long, int> vec, Stream stream)
        {
            var pos = stream.Position;

            foreach (var kvp in vec)
            {
                stream.Write(BitConverter.GetBytes(kvp.Key));
                stream.Write(BitConverter.GetBytes(kvp.Value));
            }

            return pos;
        }

        public static VectorNode DeserializeNode(byte[] nodeBuffer, Stream vectorStream)
        {
            // Deserialize node
            var vecOffset = BitConverter.ToInt64(nodeBuffer, 0);
            var postingsOffset = BitConverter.ToInt64(nodeBuffer, sizeof(long));
            var vectorCount = BitConverter.ToInt32(nodeBuffer, sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt32(nodeBuffer, sizeof(long) + sizeof(long) + sizeof(int));
            var terminator = nodeBuffer[VectorNode.BlockSize - 2];

            return DeserializeNode(vecOffset, postingsOffset, vectorCount, weight, terminator, vectorStream);
        }

        public static VectorNode DeserializeNode(
            long vecOffset,
            long postingsOffset,
            int componentCount,
            int weight,
            byte terminator,
            Stream vectorStream)
        {
            var vector = DeserializeVector(vecOffset, componentCount, vectorStream);
            var node = new VectorNode(postingsOffset, vecOffset, terminator, weight, componentCount, vector);

            return node;
        }

        public static SortedList<long, int> DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            // Deserialize term vector
            var vec = new SortedList<long, int>(componentCount);
            Span<byte> vecBuf = new byte[componentCount * VectorNode.ComponentSize];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(vecBuf);

            var offs = 0;

            for (int i = 0; i < componentCount; i++)
            {
                var key = BitConverter.ToInt64(vecBuf.Slice(offs, sizeof(long)));
                var val = BitConverter.ToInt32(vecBuf.Slice(offs + sizeof(long), sizeof(int)));

                vec.Add(key, val);

                offs += VectorNode.ComponentSize;
            }

            return vec;
        }

        public static void DeserializeUnorderedFile(
            Stream indexStream,
            Stream vectorStream,
            VectorNode root,
            (float identicalAngle, float foldAngle) similarity)
        {
            var buf = new byte[VectorNode.BlockSize];
            int read = indexStream.Read(buf);

            while (read == VectorNode.BlockSize)
            {
                var node = DeserializeNode(buf, vectorStream);

                if (node.VectorOffset > -1)
                    GraphSerializer.Add(root, node, similarity);

                read = indexStream.Read(buf);
            }
        }

        public static void DeserializeTree(
            Stream indexStream,
            Stream vectorStream,
            long indexLength,
            VectorNode root,
            (float identicalAngle, float foldAngle) similarity)
        {
            int read = 0;
            var buf = new byte[VectorNode.BlockSize];

            while (read < indexLength)
            {
                indexStream.Read(buf);

                var node = DeserializeNode(buf, vectorStream);

                if (node.VectorOffset > -1)
                    GraphSerializer.Add(root, node, similarity);

                read += VectorNode.BlockSize;
            }
        }

        public static VectorNode DeserializeTree(Stream indexStream, Stream vectorStream, long indexLength)
        {
            VectorNode root = new VectorNode();
            VectorNode cursor = root;
            var tail = new Stack<VectorNode>();
            int read = 0;
            var buf = new byte[VectorNode.BlockSize];

            while (read < indexLength)
            {
                indexStream.Read(buf);

                var node = DeserializeNode(buf, vectorStream);

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
                read += VectorNode.BlockSize;
            }

            var right = root.Right;

            right.DetachFromAncestor();

            return right;
        }
    }
}
