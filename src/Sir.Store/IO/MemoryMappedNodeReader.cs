﻿using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Index bitmap reader. Each block is a <see cref="Sir.Store.VectorNode"/>.
    /// </summary>
    public class MemoryMappedNodeReader : ILogger, INodeReader
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly MemoryMappedViewAccessor _vectorView;
        private readonly MemoryMappedFile _ixFile;
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly string _ixFileName;

        public MemoryMappedNodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config,
            MemoryMappedViewAccessor vectorView)
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _config = config;
            _vectorView = vectorView;
            _ixFile = _sessionFactory.OpenMMF(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ix"));
        }

        public Hit ClosestMatch(
            IVector vector, IStringModel model)
        {
            var pages = _sessionFactory.GetAllPages(
                Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixp"));

            var hits = new ConcurrentBag<Hit>();

            Parallel.ForEach(pages, page =>
            //foreach (var page in pages)
            {
                var hit = ClosestMatch(vector, model, page.offset);

                if (hit != null)
                    hits.Add(hit);
            });

            Hit best = null;

            foreach (var hit in hits)
            {
                if (best == null || hit.Score > best.Score)
                {
                    best = hit;
                }
                else if (hit.Score == best.Score)
                {
                    GraphBuilder.MergePostings(best.Node, hit.Node);
                }
            }

            return best;
        }

        private Hit ClosestMatch(
            IVector vector, IStringModel model, long pageOffset)
        {
            using (var segmentPageFile = new PageIndexReader(_sessionFactory.CreateReadStream(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixps"))))
            {
                var segment0 = segmentPageFile.ReadAt(pageOffset);

                var hit0 = ClosestMatchInSegment(
                    vector, _ixFile.CreateViewAccessor(segment0.offset, segment0.length), _vectorView, model, model.FoldAngle0, model.IdenticalAngle0);

                Hit hit1 = null;

                if (hit0 != null && hit0.Score > 0)
                {
                    var indexId = hit0.Node.PostingsOffsets[0];
                    var nextSegment = segmentPageFile.ReadAt(startingPoint: pageOffset, id: indexId);

                    hit1 = ClosestMatchInSegment(
                        vector, _ixFile.CreateViewAccessor(nextSegment.offset, nextSegment.length), _vectorView, model, model.FoldAngle1, model.IdenticalAngle1);
                }

                if (hit1 != null && hit1.Score > 0)
                {
                    var indexId = hit1.Node.PostingsOffsets[0];
                    var nextSegment = segmentPageFile.ReadAt(startingPoint: pageOffset, id: indexId);

                    return ClosestMatchInSegment(
                        vector, _ixFile.CreateViewAccessor(nextSegment.offset, nextSegment.length), _vectorView, model, model.FoldAngle, model.IdenticalAngle);
                }

                return null;
            }
        }

        private Hit ClosestMatchInSegment(
            IVector vector,
            MemoryMappedViewAccessor indexView,
            MemoryMappedViewAccessor vectorView,
            IStringModel model,
            double foldAngle,
            double identicalAngle,
            long offset = 0)
        {
            var block = new long[5];
            VectorNode best = null;
            double highscore = 0;

            var read = indexView.ReadArray(offset, block, 0, block.Length);

            offset += VectorNode.BlockSize;

            while (read > 0)
            {
                var vecOffset = block[0];
                var postingsOffset = block[1];
                var componentCount = block[2];
                var cursorTerminator = block[4];

                var cursorVector = model.DeserializeVector(vecOffset, (int)componentCount, vectorView);

                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= identicalAngle)
                {
                    highscore = angle;
                    best = new VectorNode(postingsOffset);

                    break;
                }
                else if (angle > foldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(postingsOffset);
                    }
                    else if (angle == highscore)
                    {
                        best.PostingsOffsets.Add(postingsOffset);
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = cursorTerminator == 0 || cursorTerminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += VectorNode.BlockSize;
                    }
                    else
                    {
                        // There is no left child.

                        break;
                    }
                }
                else
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(postingsOffset);
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        best.PostingsOffsets.Add(postingsOffset);
                    }

                    // We need to determine if we can traverse further to the right.

                    if (cursorTerminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree(indexView, ref offset);

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += VectorNode.BlockSize;
                    }
                    else if (cursorTerminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += VectorNode.BlockSize;
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

        private void SkipTree(MemoryMappedViewAccessor indexView, ref long offset)
        {
            var weight = indexView.ReadInt64(offset + sizeof(long) + sizeof(long) + sizeof(long));

            offset += VectorNode.BlockSize;

            var distance = weight * VectorNode.BlockSize;

            offset += distance;
        }

        public void Dispose()
        {
        }
    }
}