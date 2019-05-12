using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    public static class VectorNodeWriter
    {
        public static bool Add(VectorNode root, VectorNode node, (float identicalAngle, float foldAngle) similarity)
        {
            var cursor = root;

            while (cursor != null)
            {
                var angle = node.Vector.CosAngle(cursor.Vector);

                cursor.AngleWhenAdded = angle;

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
            if (target.DocIds == null)
            {
                target.DocIds = node.DocIds;
            }
            else
            {
                foreach (var id in node.DocIds)
                {
                    target.DocIds.Add(id);
                }
            }

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

        public static void Serialize(VectorNode node, Stream stream)
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

        public static (long offset, long length) SerializeTree(VectorNode node, Stream indexStream, Stream vectorStream)
        {
            var stack = new Stack<VectorNode>();
            var offset = indexStream.Position;

            while (node != null)
            {
                SerializeVector(node, vectorStream);

                Serialize(node, indexStream);

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

        public static void SerializeVector(VectorNode node, Stream vectorStream)
        {
            node.VectorOffset = node.Vector.Serialize(vectorStream);
        }

        public static IList<VectorNode> SerializePostings(VectorNode node, Stream lengths, Stream offsets, Stream lists)
        {
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
    }
}
