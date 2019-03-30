using Sir.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Index bitmap reader. Each block is a <see cref="Sir.Store.VectorNode"/>.
    /// </summary>
    public class NodeReader : ILogger
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly string _ixpFileName;
        private readonly string _ixMapName;
        private VectorNode _root;
        private readonly string _ixFileName;
        private readonly string _vecFileName;
        private long _optimizedOffset;
        private volatile bool _optimizing;

        public VectorNode Root { get { return _root; } }

        public NodeReader(
            string ixFileName,
            string ixpFileName,
            string vecFileName,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");
            _root = new VectorNode();
        }

        public void Optimize()
        {
            if (_optimizing)
                return;

            _optimizing = true;

            var pages = _sessionFactory.ReadPageInfoFromDisk(_ixpFileName);
            var last = pages[pages.Count - 1];
            var eof = last.offset + last.length;

            using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
            {
                if (_optimizedOffset > 0)
                {
                    ixStream.Seek(_optimizedOffset, SeekOrigin.Begin);
                }

                var node = ReadNode(ixStream, vectorStream);

                while (node != null)
                {
                    if (node.VectorOffset > -1)
                    {
                        _root.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
                    }

                    if (ixStream.Position < eof)
                    {
                        node = ReadNode(ixStream, vectorStream);
                    }
                    else
                    {
                        break;
                    }
                }

                _optimizedOffset = ixStream.Position;
            }

            _optimizing = false;
        }

        public Hit ClosestMatch(SortedList<long, byte> vector)
        {
            using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
            {
                if (ixStream.Length > _optimizedOffset)
                {
                    Task.Run(() => Optimize());
                }
            }

            return _root.ClosestMatch(vector, VectorNode.TermFoldAngle);
        }

        private Hit ClosestMatchInPage(
            SortedList<long, byte> node,
            Stream indexStream,
            Stream vectorStream,
            Queue<(long offset, long length)> pages)
        {
            var cursor = ReadNode(indexStream, vectorStream);

            if (cursor == null)
            {
                throw new InvalidOperationException();
            }

            var best = cursor;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = cursor.Vector.CosAngle(node);

                if (angle >= VectorNode.TermIdenticalAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    if (pages.Count == 0)
                        break; // There are no more pages.

                    // There are more pages.
                    // We can continue scanning by picking up at the first node of the next page.
                    indexStream.Seek(pages.Dequeue().offset, SeekOrigin.Begin);
                    cursor = ReadNode(indexStream, vectorStream);
                }
                else if (angle > VectorNode.TermFoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        // There is no left child.

                        if (pages.Count == 0)
                            break; // There are no more pages.

                        // There are more pages.
                        // We can continue scanning by picking up at the first node of the next page.
                        indexStream.Seek(pages.Dequeue().offset, SeekOrigin.Begin);
                        cursor = ReadNode(indexStream, vectorStream);
                    }
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further to the right.

                    if (cursor.Terminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree(indexStream);
                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        // There is no right child.

                        if (pages.Count == 0)
                            break; // There are no more pages.

                        // There are more pages.
                        // We can continue scanning by picking up at the first node of the next page.
                        indexStream.Seek(pages.Dequeue().offset, SeekOrigin.Begin);
                        cursor = ReadNode(indexStream, vectorStream);
                    }
                }
            }

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        private Hit ClosestMatchInPage(
            SortedList<long, byte> node,
            Stream indexStream,
            MemoryMappedViewAccessor vectorView)
        {
            var cursor = ReadNode(indexStream, vectorView);

            if (cursor == null)
            {
                throw new InvalidOperationException();
            }

            var best = cursor;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = cursor.Vector.CosAngle(node);

                if (angle >= VectorNode.TermIdenticalAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    break;
                }
                else if (angle > VectorNode.TermFoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        cursor = ReadNode(indexStream, vectorView);
                    }
                    else
                    {
                        // There is no left child.
                        break;
                    }
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further to the right.

                    if (cursor.Terminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree(indexStream);
                        cursor = ReadNode(indexStream, vectorView);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        cursor = ReadNode(indexStream, vectorView);
                    }
                    else
                    {
                        // There is no right child.
                        break;
                    }
                }
            }

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        private Hit ClosestMatchInPage(
            SortedList<long, byte> node,
            Stream indexStream,
            Stream vectorStream)
        {
            var cursor = ReadNode(indexStream, vectorStream);

            if (cursor == null)
            {
                throw new InvalidOperationException();
            }

            var best = cursor;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = cursor.Vector.CosAngle(node);

                if (angle >= VectorNode.TermIdenticalAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    break;
                }
                else if (angle > VectorNode.TermFoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        // There is no left child.
                        break;
                    }
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // We need to determine if we can traverse further to the right.

                    if (cursor.Terminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree(indexStream);
                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        // There is no right child.
                        break;
                    }
                }
            }

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        private VectorNode ReadNode(Stream indexStream, MemoryMappedViewAccessor vectorView)
        {
            var buf = new byte[VectorNode.NodeSize];
            var read = indexStream.Read(buf);

            if (read == 0) return null;

            var terminator = buf[buf.Length - 1];
            var node = VectorNode.DeserializeNode(buf, vectorView, ref terminator);

            return node;
        }

        private VectorNode ReadNode(Stream indexStream, Stream vectorStream)
        {
            var buf = new byte[VectorNode.NodeSize];
            var read = indexStream.Read(buf);

            if (read == 0) return null;

            var terminator = buf[buf.Length - 1];
            var node = VectorNode.DeserializeNode(buf, vectorStream, ref terminator);

            return node;
        }

        private void SkipTree(Stream indexStream)
        {
            var buf = new byte[VectorNode.NodeSize];

            var read = indexStream.Read(buf);

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var positionInBuffer = VectorNode.NodeSize - (sizeof(int) + sizeof(byte));
            var weight = BitConverter.ToInt32(buf, positionInBuffer);
            var distance = weight * VectorNode.NodeSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }
    }
}
