using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public static class GraphBuilder
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

        public static void Merge(VectorNode target, VectorNode node)
        {
            MergeDocIds(target, node);
            MergePostings(target, node);
        }

        public static void MergeDocIds(VectorNode target, VectorNode node)
        {
            foreach (var docId in node.DocIds)
            {
                target.DocIds.Add(docId);
            }

            MergePostings(target, node);
        }

        public static void MergePostings(VectorNode target, VectorNode node)
        {
            if (node.PostingsOffset >= 0)
            {
                if (target.PostingsOffset >= 0)
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
                else
                {
                    target.PostingsOffset = node.PostingsOffset;
                }
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

            while (node != null)
            {
                if (node.DocIds != null)
                {
                    SerializePostings(node, postingsStream);
                }

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
        
    }
}
