using RocksDbSharp;
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
        private readonly Stream _vectorStream;
        private readonly RocksDb _db;
        private readonly string _vecFileName;
        private readonly string _ixFileName;
        private readonly string _ixpFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config,
            RocksDb db)
        {
            _ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            _ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));
            _vecFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.vec", collectionId, keyId));

            _sessionFactory = sessionFactory;
            _config = config;
            _vectorStream = _sessionFactory.CreateReadStream(_vecFileName);
            _db = db;
        }

        public Hit ClosestMatch(Vector vector, IStringModel model)
        {
            var hits = ClosestMatchOnDisk(vector, model);
            var time = Stopwatch.StartNew();
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

            this.Log($"merge took {time.Elapsed}");

            return best;
        }

        private IList<Hit> ClosestMatchOnDisk(
            Vector vector, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var pages = _sessionFactory.ReadPageInfo(_ixpFileName);
            var hits = new List<Hit>();

            foreach (var page in pages)
            {
                var hit = ClosestMatchInPage(
                            vector,
                            _db,
                            _vectorStream,
                            model,
                            page.offset,
                            page.length);

                hits.Add(hit);
            }

            this.Log($"scan took {time.Elapsed}");

            return hits;
        }

        private Hit ClosestMatchInPage(
            Vector vector,
            RocksDb db,
            Stream vectorStream,
            IStringModel model,
            long pageOffset,
            long pageLength
        )
        {
            Span<byte> block = stackalloc byte[VectorNode.BlockSize];

            var read = indexStream.Read(block);

            VectorNode best = null;
            float highscore = 0;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0, sizeof(long)));
                var componentCount = BitConverter.ToInt64(block.Slice(sizeof(long) + sizeof(long), sizeof(long)));
                var cursorVector = model.DeserializeVector(vecOffset, (int)componentCount, vectorStream);
                var cursorTerminator = BitConverter.ToInt64(block.Slice(sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long), sizeof(long)));
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long), sizeof(long)));
                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.IdenticalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
                    }

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
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
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
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

        private Hit ClosestMatchInPage(
            Vector vector,
            Stream indexStream,
            Stream vectorStream,
            IStringModel model
        )
        {
            Span<byte> block = stackalloc byte[VectorNode.BlockSize];

            var read = indexStream.Read(block);

            VectorNode best = null;
            float highscore = 0;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0, sizeof(long)));
                var componentCount = BitConverter.ToInt64(block.Slice(sizeof(long) + sizeof(long), sizeof(long)));
                var cursorVector = model.DeserializeVector(vecOffset, (int)componentCount, vectorStream);
                var cursorTerminator = BitConverter.ToInt64(block.Slice(sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long), sizeof(long)));
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long), sizeof(long)));
                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.IdenticalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
                    }

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
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
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
                    }
                    else if (angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, postingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(postingsOffset);
                        }
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

        private void SkipTree(RocksDb db, )
        {
            db.
            Span<VectorNodeData> buf = new VectorNodeData[1];

            var read = indexStream.Read(buf);

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var positionInBuffer = VectorNode.BlockSize - (sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt64(buf.Slice(positionInBuffer, sizeof(long)));
            var distance = weight * VectorNode.BlockSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }

        private void SkipTree(Stream indexStream)
        {
            Span<byte> buf = new byte[VectorNode.BlockSize];

            var read = indexStream.Read(buf);

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var positionInBuffer = VectorNode.BlockSize - (sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt64(buf.Slice(positionInBuffer, sizeof(long)));
            var distance = weight * VectorNode.BlockSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }

        public void Dispose()
        {
            _indexStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}
