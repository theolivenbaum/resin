using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Index bitmap reader. Each block is a <see cref="Sir.Search.VectorNode"/>.
    /// </summary>
    public class ColumnStreamReader : IColumnReader
    {
        private readonly ISessionFactory _sessionFactory;
        private readonly ILogger _logger;
        private readonly ulong _collectionId;
        private readonly Stream _vectorFile;
        private readonly Stream _ixFile;
        private readonly IList<(long offset, long length)> _segments;

        public ColumnStreamReader(
            ulong collectionId,
            long keyId,
            ISessionFactory sessionFactory,
            ILogger logger)
        {
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _logger = logger;

            var vectorFileName = Path.Combine(_sessionFactory.Dir, $"{_collectionId}.vec");
            var ixFileName = Path.Combine(_sessionFactory.Dir, string.Format("{0}.{1}.ix", _collectionId, keyId));

            _vectorFile = _sessionFactory.CreateReadStream(vectorFileName);
            _ixFile = _sessionFactory.CreateReadStream(ixFileName);
            _segments = GetAllSegments(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{keyId}.ixtp"));
        }

        public Hit ClosestMatch(IVector vector, IModel model)
        {
            var time = Stopwatch.StartNew();
            var hits = new List<Hit>();

            foreach (var segment in _segments)
            {
                var hit = ClosestMatchInSegment(vector, model, segment.offset, segment.length);

                if (hit.Score > 0)
                {
                    hits.Add(hit);
                }
            }

            _logger.LogDebug($"scanning all segments took {time.Elapsed}");

            Hit best = null;

            foreach (var hit in hits)
            {
                if (best == null || hit.Score > best.Score)
                {
                    best = hit;
                }
                else if (hit.Score.Approximates(best.Score))
                {
                    GraphBuilder.MergePostings(best.Node, hit.Node);
                }
            }

            return best;
        }

        private Hit ClosestMatchInSegment(IVector queryVector, IModel model, long segmentOffset, long segmentSize)
        {
            _ixFile.Seek(segmentOffset, SeekOrigin.Begin);

            Span<byte> block = stackalloc byte[VectorNode.BlockSize];
            var best = new Hit();
            var read = _ixFile.Read(block);

            while (read < segmentSize)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0));
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long)));
                var componentCount = BitConverter.ToInt64(block.Slice(sizeof(long) * 2));
                var terminator = BitConverter.ToInt64(block.Slice(sizeof(long) * 4));
                var angle = model.CosAngle(queryVector, vecOffset, (int)componentCount, _vectorFile);

                if (angle >= model.IdenticalAngle)
                {
                    best.Score = angle;
                    var n = new VectorNode(postingsOffset);
                    best.Node = n;

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (best == null || angle > best.Score)
                    {
                        best.Score = angle;
                        best.Node = new VectorNode(postingsOffset);
                    }
                    else if (angle == best.Score)
                    {
                        best.Node.PostingsOffsets.Add(postingsOffset);
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = terminator == 0 || terminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        read += _ixFile.Read(block);
                    }
                    else
                    {
                        // There is no left child.

                        break;
                    }
                }
                else
                {
                    if ((best == null && angle > best.Score) || angle > best.Score)
                    {
                        best.Score = angle;
                        best.Node = new VectorNode(postingsOffset);
                    }
                    else if (angle > 0 && angle == best.Score)
                    {
                        best.Node.PostingsOffsets.Add(postingsOffset);
                    }

                    // We need to determine if we can traverse further to the right.

                    if (terminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree();

                        read += _ixFile.Read(block);
                    }
                    else if (terminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        read += _ixFile.Read(block);
                    }
                    else
                    {
                        // There is no right child.

                        break;
                    }
                }
            }

            return best;
        }

        private void SkipTree()
        {
            Span<byte> buf = stackalloc byte[VectorNode.BlockSize];
            var read = _ixFile.Read(buf);
            var sizeOfTree = BitConverter.ToInt64(buf.Slice(sizeof(long) * 3));
            var distance = sizeOfTree * VectorNode.BlockSize;

            if (distance > 0)
            {
                _ixFile.Seek(distance, SeekOrigin.Current);
            }
        }

        private IList<(long offset, long length)> GetAllSegments(string pageFileName)
        {
            using (var stream = _sessionFactory.CreateReadStream(pageFileName))
            {
                return new PageIndexReader(stream).GetAll();
            }
        }

        public void Dispose()
        {
            _vectorFile.Dispose();
            _ixFile.Dispose();
        }
    }
}
