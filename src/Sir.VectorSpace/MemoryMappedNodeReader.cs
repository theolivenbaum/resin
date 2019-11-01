using Sir.VectorSpace;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ISessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly MemoryMappedViewAccessor _vectorView;
        private readonly MemoryMappedFile _ixFile;
        private readonly long _keyId;
        private readonly ulong _collectionId;

        public long KeyId { get { return _keyId; } }

        public MemoryMappedNodeReader(
            ulong collectionId,
            long keyId,
            ISessionFactory sessionFactory,
            IConfigurationProvider config,
            MemoryMappedViewAccessor vectorView)
        {
            _keyId = keyId;
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _config = config;
            _vectorView = vectorView;
            _ixFile = _sessionFactory.OpenMMF(
                Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ix"));
        }

        public Hit ClosestTerm(
            IVector vector, IStringModel model)
        {
            var pages = GetAllPages(
                Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixtp"));

            var hits = new ConcurrentBag<Hit>();

            Parallel.ForEach(pages, page =>
            //foreach (var page in pages)
            {
                var hit = ClosestTermInPage(vector, model, page.offset, page.length);

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
                else if (hit.Score >= model.IdenticalAngle)
                {
                    GraphBuilder.MergePostings(best.Node, hit.Node);
                }
            }

            return best;
        }

        public IList<(long offset, long length)> GetAllPages(string pageFileName)
        {
            using (var ixpStream = _sessionFactory.CreateReadStream(pageFileName))
            {
                return new PageIndexReader(ixpStream).GetAll();
            }
        }

        private Hit ClosestTermInPage(
            IVector vector, IStringModel model, long pageOffset, long length)
        {
            var hit0 = ClosestMatchInSegment(
                    vector,
                    _ixFile.CreateViewAccessor(pageOffset, length),
                    _vectorView,
                    model,
                    model.FoldAngle,
                    model.IdenticalAngle);

            if (hit0.Score > 0)
            {
                return hit0;
            }

            return null;
        }

        private Hit ClosestMatchInSegment(
            IVector queryVector,
            MemoryMappedViewAccessor indexView,
            MemoryMappedViewAccessor vectorView,
            IEuclidDistance model,
            double foldAngle,
            double identicalAngle,
            long offset = 0)
        {
            const int blockLength = 5;
            long[] block = ArrayPool<long>.Shared.Rent(blockLength);
            var best = new Hit();
            var read = indexView.ReadArray(offset, block, 0, blockLength);

            offset += VectorNode.BlockSize;

            while (read > 0)
            {
                var vecOffset = block[0];
                var postingsOffset = block[1];
                var componentCount = block[2];
                var cursorTerminator = block[4];
                var indexVector = VectorOperations.DeserializeVector(vecOffset, (int)componentCount, model.VectorWidth, vectorView);
                var angle = model.CosAngle(indexVector, queryVector);

                if (angle >= identicalAngle)
                {
                    best.Score = angle;
                    var n = new VectorNode(postingsOffset);
                    best.Node = n;

                    break;
                }
                else if (angle > foldAngle)
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
                    bool canGoLeft = cursorTerminator == 0 || cursorTerminator == 1;

                    if (canGoLeft)
                    {
                        // There exists either a left and a right child or just a left child.
                        // Either way, we want to go left and the next node in bitmap is the left child.

                        read = indexView.ReadArray(offset, block, 0, blockLength);

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

                    if (cursorTerminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        SkipTree(indexView, ref offset);

                        read = indexView.ReadArray(offset, block, 0, blockLength);

                        offset += VectorNode.BlockSize;
                    }
                    else if (cursorTerminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        read = indexView.ReadArray(offset, block, 0, blockLength);

                        offset += VectorNode.BlockSize;
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

        private void SkipTree(MemoryMappedViewAccessor indexView, ref long offset)
        {
            var weight = indexView.ReadInt64(offset + sizeof(long) + sizeof(long) + sizeof(long));

            offset += VectorNode.BlockSize;

            var distance = weight * VectorNode.BlockSize;

            offset += distance;
        }

        public void Dispose()
        {
            _vectorView.Dispose();
        }
    }
}
