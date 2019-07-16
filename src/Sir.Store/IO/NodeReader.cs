using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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
        private readonly ulong _collectionId;
        private readonly string _ixFileName;
        private readonly string _ixsFileName;
        private readonly string _ixspFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            var ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            var ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));

            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            _ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));
            _ixsFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixs", collectionId, keyId));
            _ixspFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixsp", collectionId, keyId));
            _vecFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId));
        }

        public Hit ClosestMatch(IVector vector, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var best = ClosestMatchOnDisk(vector, model);

            this.Log($"scan took {time.Elapsed}");

            return best;
        }

        private Hit ClosestMatchOnDisk(
            IVector vector, IStringModel model)
        {
            int indexId;
            var bufferSize = int.Parse(_config.Get("nodereader_buffer_size"));
            var pages = _sessionFactory.ReadPageInfo(_ixspFileName);

            using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            {
                using (var indexStream = _sessionFactory.CreateReadStream(_ixFileName, bufferSize: bufferSize, fileOptions: FileOptions.RandomAccess))
                {
                    var hit = ClosestMatchInPrimaryPage(vector, indexStream, vectorStream, model);
                    indexId = (int)hit.Node.PostingsOffsets[0];
                }

                var page = pages[indexId];

                using (var indexStream = _sessionFactory.CreateReadStream(_ixsFileName, bufferSize: bufferSize, fileOptions: FileOptions.RandomAccess))
                {
                    indexStream.Seek(page.offset, SeekOrigin.Begin);

                    return ClosestMatchInPage(vector, indexStream, vectorStream, model);
                }
            }
        }

        private Hit ClosestMatchInPage(
            IVector vector,
            Stream indexStream,
            Stream vectorStream,
            IStringModel model
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

                if (angle >= model.IdenticalAngle)
                {
                    highscore = angle;
                    best = new VectorNode(postingsOffset);

                    break;
                }
                else if (angle > model.FoldAngle)
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

        private Hit ClosestMatchInPrimaryPage(
            IVector vector,
            Stream indexStream,
            Stream vectorStream,
            IStringModel model
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
                var componentCount = BitConverter.ToInt64(block.Slice(sizeof(long) * 2));
                var cursorTerminator = BitConverter.ToInt64(block.Slice(sizeof(long) * 4));
                var angle = model.CosAngle(vector, vecOffset, (int)componentCount, vectorStream);

                if (angle >= model.PrimaryIndexIdenticalAngle)
                {
                    highscore = angle;
                    best = new VectorNode(postingsOffset);

                    break;
                }
                else if (angle > model.PrimaryIndexFoldAngle)
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
        }
    }
}
