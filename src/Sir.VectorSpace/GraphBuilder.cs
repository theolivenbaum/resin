using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    public static class GraphBuilder
    {
        public static VectorNode CreateTree<T>(this IModel<T> model, IIndexingStrategy indexingStrategy, params T[] data)
        {
            var root = new VectorNode();

            foreach (var item in data)
            {
                foreach (var vector in model.Tokenize(item))
                {
                    indexingStrategy.ExecutePut<T>(root, new VectorNode(vector));
                }
            }

            return root;
        }

        public static void MergeOrAddSupervised(
            this VectorNode root,
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    if (!cursor.Vector.Label.Equals(node.Vector.Label))
                        throw new InvalidOperationException($"IdenticalAngle {model.IdenticalAngle} is too low. Angle was {angle}");

                    MergeDocIds(cursor, node);
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

        public static void MergeOrAdd(
            this VectorNode root, 
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    MergeDocIds(cursor, node);

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

        public static void AddIfUnique(
            this VectorNode root,
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
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

        public static bool TryAdd(
            this VectorNode root,
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    return false;
                }
                else if (angle > model.FoldAngle)
                {
                    if (cursor.Left == null)
                    {
                        cursor.Left = node;

                        return true;
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

                        return true;
                    }
                    else
                    {
                        cursor = cursor.Right;
                    }
                }
            }
        }

        public static void Build(
            this VectorNode root,
            VectorNode node,
            IModel model)
        {
            var cursor = root;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
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

        public static void MergePostings(this VectorNode target, VectorNode source)
        {
            if (source.PostingsOffsets != null)
                ((List<long>)target.PostingsOffsets).AddRange(source.PostingsOffsets);
        }

        public static void MergeDocIds(this VectorNode target, VectorNode source)
        {
            if (source.DocIds != null)
            {
                target.DocIds.AddRange(source.DocIds);
            }
        }

        public static void MergeDocIdsConcurrent(this VectorNode target, VectorNode source)
        {
            lock (target.Sync)
            {
                if (source.DocIds != null)
                {
                    target.DocIds.AddRange(source.DocIds);
                }
            }
        }

        public static void Serialize(this VectorNode node, Stream stream)
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
        public static (long offset, long length) SerializeTree(this VectorNode node, Stream indexStream, Stream vectorStream, Stream postingsStream = null)
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

                Serialize(node, indexStream);

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

        public static void SerializePostings(this VectorNode node, Stream postingsStream)
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
    }
}