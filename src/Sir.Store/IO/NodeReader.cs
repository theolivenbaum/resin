using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Index bitmap reader. Each block is a <see cref="Sir.Store.VectorNode"/>.
    /// </summary>
    public class NodeReader : ILogger, IDisposable
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly string _ixpFileName;
        private readonly string _vecFileName;
        private readonly Stream _vectorStream;
        private readonly Stream _ixStream;
        private readonly PageIndexReader _segmentPageFile;
        private readonly long _keyId;
        private readonly ulong _collectionId;
        private readonly string _ixFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            var ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            var ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));

            _keyId = keyId;
            _collectionId = collectionId;
            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _vecFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId));
            _vectorStream = _sessionFactory.CreateReadStream(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.vec"));
            _ixStream = _sessionFactory.CreateReadStream(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ix"), bufferSize: int.Parse(_config.Get("nodereader_buffer_size")), fileOptions: FileOptions.RandomAccess);
            _segmentPageFile = new PageIndexReader(_sessionFactory.CreateReadStream(Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixps")));
        }

        public Hit ClosestMatch(
            IVector vector, IStringModel model)
        {
            var pages = _sessionFactory.GetAllPages(
                Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixp"));

            var hits = new List<Hit>();

            foreach (var page in pages)
            {
                var hit = ClosestMatch(vector, model, page.offset);

                if (hit != null)
                    hits.Add(hit);
            }

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
            var pages = _sessionFactory.GetAllPages(
                Path.Combine(_sessionFactory.Dir, $"{_collectionId}.{_keyId}.ixp"));

            var ix0page = _segmentPageFile.ReadAt(pageOffset);

            _ixStream.Seek(ix0page.offset, SeekOrigin.Begin);

            var hit0 = ClosestMatchInPage(
                vector, _ixStream, _vectorStream, model, model.FoldAngle0, model.IdenticalAngle0);

            Hit hit1 = null;

            if (hit0 != null && hit0.Score > 0)
            {
                var indexId = hit0.Node.PostingsOffsets[0];
                var nextSegment = _segmentPageFile.ReadAt(startingPoint: pageOffset, id: indexId);

                _ixStream.Seek(nextSegment.offset, SeekOrigin.Begin);

                hit1 = ClosestMatchInPage(
                    vector, _ixStream, _vectorStream, model, model.FoldAngle1, model.IdenticalAngle1);
            }

            if (hit1 != null && hit1.Score > 0)
            {
                var indexId = hit1.Node.PostingsOffsets[0];
                var nextSegment = _segmentPageFile.ReadAt(startingPoint: pageOffset, id: indexId);

                _ixStream.Seek(nextSegment.offset, SeekOrigin.Begin);

                return ClosestMatchInPage(
                    vector, _ixStream, _vectorStream, model, model.FoldAngle, model.IdenticalAngle);
            }

            return null;
        }

        private Hit ClosestMatchInPage(
            IVector vector,
            Stream indexStream,
            Stream vectorStream,
            IStringModel model,
            double foldAngle,
            double identicalAngle
        )
        {
            Span<byte> block = stackalloc byte[VectorNode.BlockSize];
            var read = indexStream.Read(block);
            VectorNode best = null;
            double highscore = 0;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0));
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long)));
                var componentCount = BitConverter.ToInt64(block.Slice(sizeof(long)*2));
                var cursorTerminator = BitConverter.ToInt64(block.Slice(sizeof(long) * 4));
                var angle = model.CosAngle(vector, vecOffset, (int)componentCount, vectorStream);

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

                        read = indexStream.Read(block);
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

                        SkipTree(indexStream);
                        read = indexStream.Read(block);
                    }
                    else if (cursorTerminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        read = indexStream.Read(block);
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

        private void SkipTree(Stream indexStream)
        {
            Span<byte> buf = stackalloc byte[VectorNode.BlockSize];
            var read = indexStream.Read(buf);
            var weight = BitConverter.ToInt64(buf.Slice(sizeof(long)*3));
            var distance = weight * VectorNode.BlockSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }

        public void Dispose()
        {
            _vectorStream.Dispose();
            _ixStream.Dispose();
            _segmentPageFile.Dispose();
        }
    }
}
