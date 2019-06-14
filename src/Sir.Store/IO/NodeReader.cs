using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        private readonly string _ixFileName;
        private readonly string _vecFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            var ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            var ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));
            var vecFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.vec", collectionId, keyId));

            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");
        }

        public Hit ClosestMatch(Vector vector, IStringModel model)
        {
            var hits = ClosestMatchInMemory(vector, model);
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
            var ixbufferSize = int.Parse(_config.Get("index_read_buffer_size") ?? "4096");

            using (var indexStream = new BufferedStream(_sessionFactory.CreateReadStream(_ixFileName), ixbufferSize))
            using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            {
                foreach (var page in pages)
                {
                    indexStream.Seek(page.offset, SeekOrigin.Begin);

                    var hit = ClosestMatchInPage(
                                vector,
                                indexStream,
                                vectorStream,
                                model);

                    hits.Add(hit);
                }
            }

            this.Log($"scan took {time.Elapsed}");

            return hits;
        }

        private ConcurrentBag<Hit> ClosestMatchInMemory(
            Vector vector, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var pages = _sessionFactory.ReadPageInfo(_ixpFileName);
            var hits = new ConcurrentBag<Hit>();
            var ixFile = _sessionFactory.OpenMMF(_ixFileName);
            var vecFile = _sessionFactory.OpenMMF(_vecFileName);

            using (var vectorView = vecFile.CreateViewAccessor(0, 0))
            //foreach (var page in pages)
            Parallel.ForEach(pages, page =>
            {
                using (var indexView = ixFile.CreateViewAccessor(page.offset, page.length))
                {
                    var hit = ClosestMatchInPage(
                                vector,
                                indexView,
                                vectorView,
                                model);

                    hits.Add(hit);
                }
            });

            this.Log($"scan took {time.Elapsed}");

            return hits;
        }

        private Hit ClosestMatchInPage(
            Vector vector,
            Stream indexStream,
            Stream vectorStream,
            IStringModel model
        )
        {
            Span<byte> block = new byte[VectorNode.BlockSize];

            var read = indexStream.Read(block);

            VectorNode best = null;
            float highscore = 0;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0, sizeof(long)));
                var componentCount = BitConverter.ToInt32(block.Slice(sizeof(long) + sizeof(long), sizeof(int)));
                var cursorVector = model.DeserializeVector(vecOffset, componentCount, vectorStream);
                var cursorTerminator = block[block.Length - 1];
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long), sizeof(long)));

                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.Similarity().identicalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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
                else if (angle > model.Similarity().foldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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
                        best.PostingsOffset = postingsOffset;
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
            MemoryMappedViewAccessor vectorView,
            IStringModel model
        )
        {
            Span<byte> block = new byte[VectorNode.BlockSize];

            var read = indexStream.Read(block);

            VectorNode best = null;
            float highscore = 0;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block.Slice(0, sizeof(long)));
                var componentCount = BitConverter.ToInt32(block.Slice(sizeof(long) + sizeof(long), sizeof(int)));
                var cursorVector = model.DeserializeVector(vecOffset, componentCount, vectorView);
                var cursorTerminator = block[block.Length - 1];
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long), sizeof(long)));

                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.Similarity().identicalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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
                else if (angle > model.Similarity().foldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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
                        best.PostingsOffset = postingsOffset;
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
            MemoryMappedViewAccessor indexView,
            MemoryMappedViewAccessor vectorView,
            IStringModel model
        )
        {
            long offset = 0;
            var block = new byte[VectorNode.BlockSize];
            VectorNode best = null;
            float highscore = 0;

            var read = indexView.ReadArray(offset, block, 0, block.Length);

            offset += block.Length;

            while (read > 0)
            {
                var vecOffset = BitConverter.ToInt64(block);
                var componentCount = BitConverter.ToInt32(block, sizeof(long) + sizeof(long));
                var cursorVector = model.DeserializeVector(vecOffset, componentCount, vectorView);
                var cursorTerminator = block[block.Length - 1];
                var postingsOffset = BitConverter.ToInt64(block, sizeof(long));

                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.Similarity().identicalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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
                else if (angle > model.Similarity().foldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector);
                        best.PostingsOffset = postingsOffset;
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

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += block.Length;
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
                        best.PostingsOffset = postingsOffset;
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

                        SkipTree(indexView, ref offset);

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += block.Length;
                    }
                    else if (cursorTerminator == 2)
                    {
                        // Next node in bitmap is the right child,
                        // which is good because we want to go right.

                        read = indexView.ReadArray(offset, block, 0, block.Length);

                        offset += block.Length;
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
            Span<byte> buf = new byte[VectorNode.BlockSize];

            var read = indexStream.Read(buf);

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var positionInBuffer = VectorNode.BlockSize - (sizeof(int) + sizeof(byte));
            var weight = BitConverter.ToInt32(buf.Slice(positionInBuffer, sizeof(int)));
            var distance = weight * VectorNode.BlockSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }

        private void SkipTree(MemoryMappedViewAccessor indexView, ref long offset)
        {
            var buf = new byte[VectorNode.BlockSize];
            var read = indexView.ReadArray(offset, buf, 0, buf.Length);

            offset += buf.Length;

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var weight = BitConverter.ToInt32(buf, sizeof(long) + sizeof(long) + sizeof(int));
            var distance = weight * VectorNode.BlockSize;

            offset += distance;
        }
    }
}
