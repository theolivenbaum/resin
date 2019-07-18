using System;
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
        private readonly string _ix3pFileName;
        private readonly string _vecFileName;
        private readonly ulong _collectionId;
        private readonly string _ix3FileName;
        private readonly string _ix2FileName;
        private readonly string _ix2pFileName;
        private readonly string _ix1FileName;
        private readonly string _ix1pFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            _collectionId = collectionId;
            _sessionFactory = sessionFactory;
            _config = config;
            _ix3FileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix3", collectionId, keyId));
            _ix3pFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix3p", collectionId, keyId));
            _ix2FileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix2", collectionId, keyId));
            _ix2pFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix2p", collectionId, keyId));
            _ix1FileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix1", collectionId, keyId));
            _ix1pFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix1p", collectionId, keyId));
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
            var bufferSize = int.Parse(_config.Get("nodereader_buffer_size"));
            var level2Pages = _sessionFactory.ReadPageInfo(_ix2pFileName);
            var level3Pages = _sessionFactory.ReadPageInfo(_ix3pFileName);

            using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            {
                int level2Index;
                int level3Index;

                using (var level1IndexStream = _sessionFactory.CreateReadStream(_ix1FileName, bufferSize: bufferSize, fileOptions: FileOptions.RandomAccess))
                {
                    var hit = ClosestMatchInPage(vector, level1IndexStream, vectorStream, model.Level1IdenticalAngle, model.Level1FoldAngle, model);

                    if (hit.Score > 0)
                        level2Index = (int)hit.Node.PostingsOffsets[0];
                    else
                        return hit;
                }

                var level2Page = level2Pages[level2Index];

                using (var level2IndexStream = _sessionFactory.CreateReadStream(_ix2FileName, bufferSize: bufferSize, fileOptions: FileOptions.RandomAccess))
                {
                    level2IndexStream.Seek(level2Page.offset, SeekOrigin.Begin);

                    var hit = ClosestMatchInPage(vector, level2IndexStream, vectorStream, model.Level2IdenticalAngle, model.Level2FoldAngle, model);

                    if (hit.Score > 0)
                        level3Index = (int)hit.Node.PostingsOffsets[0];
                    else
                        return hit;
                }

                var level3Page = level3Pages[level3Index];

                using (var level3IndexStream = _sessionFactory.CreateReadStream(_ix3FileName, bufferSize: bufferSize, fileOptions: FileOptions.RandomAccess))
                {
                    level3IndexStream.Seek(level3Page.offset, SeekOrigin.Begin);

                    return ClosestMatchInPage(vector, level3IndexStream, vectorStream, model.Level3IdenticalAngle, model.Level3FoldAngle, model);
                }
            }
        }

        private Hit ClosestMatchInPage(
            IVector vector,
            Stream indexStream,
            Stream vectorStream,
            double identicalAngle,
            double foldAngle,
            IDistance model
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
        }
    }
}
