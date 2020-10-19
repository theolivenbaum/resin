using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    public static class GraphBuilder
    {
        public static VectorNode CreateTree<T>(IModel<T> model, IIndexingStrategy indexingStrategy, params T[] data)
        {
            var root = new VectorNode();
            var unclassified = new Queue<(long keyId, VectorNode node)>();
            const long columnId = 0;

            foreach (var item in data)
            {
                foreach (var vector in model.Tokenize(item))
                {
                    indexingStrategy.ExecutePut(root, columnId, new VectorNode(vector), model, unclassified);
                }
            }

            indexingStrategy.ExecuteFlush(new Dictionary<long, VectorNode>() { { columnId, root } }, unclassified);

            return root;
        }

        public static bool TryRecalculateVectorOrAdd(
            VectorNode root,
            VectorNode node,
            IModel model,
            int maxDepth,
            out VectorNode unclassified)
        {
            var cursor = root;
            var depth = 0;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    if (node.Vector.Label.Equals(cursor.Vector.Label))
                    {
                        AddDocId(cursor, node);
                        cursor.Vector.AverageInPlace(node.Vector);
                        unclassified = null;
                        return true;
                    }
                    else
                    {
                        unclassified = node;
                        return false;
                    }
                }
                else if (angle > model.FoldAngle)
                {
                    if (node.Vector.Label.Equals(cursor.Vector.Label))
                    {
                        if (depth < maxDepth)
                        {
                            if (cursor.Left == null)
                            {
                                cursor.Left = node;
                                unclassified = null;
                                return true;
                            }
                            else
                            {
                                cursor = cursor.Left;
                                depth++;
                            }
                        }
                        else
                        {
                            AddDocId(cursor, node);
                            cursor.Vector.AverageInPlace(node.Vector.Multiply((float)angle/2));
                            unclassified = null;
                            return true;
                        }
                    }
                    else
                    {
                        unclassified = node;
                        return false;
                    }
                }
                else
                {
                    if (cursor.Right == null)
                    {
                        cursor.Right = node;
                        unclassified = null;
                        return true;
                    }
                    else
                    {
                        cursor = cursor.Right;
                        depth = Math.Max(0, depth-1);
                    }
                }
            }
        }

        public static bool TryRecalculateVectorOrAdd(
            VectorNode root,
            VectorNode node,
            IModel model,
            out VectorNode unclassified)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);
                
                if (angle > model.FoldAngle)
                {
                    if (node.Vector.Label.Equals(cursor.Vector.Label))
                    {
                        AddDocId(cursor, node);
                        cursor.Vector.AverageInPlace(node.Vector);
                        unclassified = null;
                        return true;
                    }
                    else
                    {
                        unclassified = node;
                        return false;
                    }
                }
                else
                {
                    if (cursor.Right == null)
                    {
                        cursor.Right = node;
                        unclassified = null;
                        return true;
                    }
                    else
                    {
                        cursor = cursor.Right;
                    }
                }
            }
        }

        public static void MergeOrAdd(
            VectorNode root, 
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    AddDocId(cursor, node);

                    break;
                }
                else if (angle > model.FoldAngle)
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
                else
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
        }

        public static void MergeOrAddConcurrent(
            VectorNode root,
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    lock (cursor.Sync)
                    {
                        AddDocId(cursor, node);
                    }

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (cursor.Left == null)
                    {
                        lock (cursor.Sync)
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
                        lock (cursor.Sync)
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

        public static void InsertRight(VectorNode parent, VectorNode node)
        {
            node.Right = parent.Right;
            parent.Right = node;
        }

        public static void AddRight(VectorNode parent, VectorNode node)
        {
            var target = parent;

            while(target.Right != null)
            {
                target = target.Right;
            }

            node.Right = target.Right;
            target.Right = node;
        }

        public static void MergePostings(VectorNode target, VectorNode source)
        {
            if (source.PostingsOffsets != null)
                ((List<long>)target.PostingsOffsets).AddRange(source.PostingsOffsets);
        }

        public static void AddDocId(VectorNode target, long docId)
        {
            target.DocIds.Add(docId);
        }

        public static void AddDocId(VectorNode target, VectorNode node)
        {
            if (target.DocIds != null && node.DocIds != null)
            {
                foreach (var docId in node.DocIds)
                {
                    target.DocIds.Add(docId);
                }
            }
        }

        public static void SerializeNode(VectorNode node, Stream stream)
        {
            long terminator = 1;

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

            Span<long> span = stackalloc long[5];

            span[0] = node.VectorOffset;
            span[1] = node.PostingsOffset;
            span[2] = node.Vector.ComponentCount;
            span[3] = node.Weight;
            span[4] = terminator;

            stream.Write(MemoryMarshal.Cast<long, byte>(span));
        }

        /// <summary>
        /// Persist tree to disk.
        /// </summary>
        /// <param name="node">Tree to perist.</param>
        /// <param name="indexStream">stream to perist tree into</param>
        /// <param name="vectorStream">stream to persist vectors in</param>
        /// <param name="postingsStream">optional stream to persist any posting references into</param>
        /// <returns></returns>
        public static (long offset, long length) SerializeTree(
            VectorNode node, 
            Stream indexStream, 
            Stream vectorStream, 
            Stream postingsStream = null)
        {
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;
            var length = 0;

            if (node.ComponentCount == 0)
            {
                node = node.Right;
            }

            while (node != null)
            {
                if (node.PostingsOffset == -1 && postingsStream != null)
                    SerializePostings(node, postingsStream);

                node.VectorOffset = VectorOperations.SerializeVector(node.Vector, vectorStream);

                SerializeNode(node, indexStream);

                length += VectorNode.BlockSize;

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

            return (offset, length);
        }

        public static void SerializePostings(VectorNode node, Stream postingsStream)
        {
            node.PostingsOffset = postingsStream.Position;

            SerializeHeaderAndPayload(node.DocIds, node.DocIds.Count, postingsStream);
        }

        public static void SerializeHeaderAndPayload(IEnumerable<long> items, int itemCount, Stream stream)
        {
            var payload = new long[itemCount + 1];

            payload[0] = itemCount;

            var index = 1;

            foreach (var item in items)
            {
                payload[index++] = item;
            }

            stream.Write(MemoryMarshal.Cast<long, byte>(payload));
        }

        public static VectorNode DeserializeNode(byte[] nodeBuffer, Stream vectorStream, IModel model)
        {
            // Deserialize node
            var vecOffset = BitConverter.ToInt64(nodeBuffer, 0);
            var postingsOffset = BitConverter.ToInt64(nodeBuffer, sizeof(long));
            var vectorCount = BitConverter.ToInt64(nodeBuffer, sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt64(nodeBuffer, sizeof(long) + sizeof(long) + sizeof(long));
            var terminator = BitConverter.ToInt64(nodeBuffer, sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long));

            return DeserializeNode(vecOffset, postingsOffset, vectorCount, weight, terminator, vectorStream, model);
        }

        public static VectorNode DeserializeNode(
            long vecOffset,
            long postingsOffset,
            long componentCount,
            long weight,
            long terminator,
            Stream vectorStream,
            IDistanceCalculator model)
        {
            var vector = VectorOperations.DeserializeVector(vecOffset, (int)componentCount, model.VectorWidth, vectorStream);
            var node = new VectorNode(postingsOffset, vecOffset, terminator, weight, vector);

            return node;
        }

        public static VectorNode DeserializeTree(Stream indexStream, Stream vectorStream, long indexLength, IModel model)
        {
            VectorNode root = new VectorNode();
            VectorNode cursor = root;
            var tail = new Stack<VectorNode>();
            int read = 0;
            var buf = new byte[VectorNode.BlockSize];

            while (read < indexLength)
            {
                indexStream.Read(buf);

                var node = DeserializeNode(buf, vectorStream, model);

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

            return root;
        }
    }
}
