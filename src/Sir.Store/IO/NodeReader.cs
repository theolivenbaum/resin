using System;
using System.Buffers;
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
            var hits = ClosestMatchInMemoryMap(vector, model);
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

            using (var indexStream = _sessionFactory.CreateReadStream(_ixFileName))
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
            var hits = new ConcurrentBag<Hit>();
            var hit = PathFinder.ClosestMatch(_sessionFactory.GetGraph(_ixFileName), vector, model);

            hits.Add(hit);

            this.Log($"scan took {time.Elapsed}");

            return hits;
        }

        private ConcurrentBag<Hit> ClosestMatchInMemoryMap(
            Vector vector, IStringModel model)
        {
            var time = Stopwatch.StartNew();
            var pages = _sessionFactory.ReadPageInfo(_ixpFileName);
            var hits = new ConcurrentBag<Hit>();
            var ixFile = _sessionFactory.OpenMMF(_ixFileName);
            var vecFile = _sessionFactory.OpenMMF(_vecFileName);

            using (var vectorView = vecFile.CreateViewAccessor(0, 0))
            using (var indexView = ixFile.CreateViewAccessor(0, 0))

            //foreach (var page in pages)
            Parallel.ForEach(pages, page =>
            {
                var hit = ClosestMatchInPage(
                                vector,
                                indexView,
                                vectorView,
                                model,
                                page.offset);

                hits.Add(hit);
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
            MemoryMappedViewAccessor vectorView,
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
                var cursorVector = model.DeserializeVector(vecOffset, (int)componentCount, vectorView);
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
            MemoryMappedViewAccessor indexView,
            MemoryMappedViewAccessor vectorView,
            IStringModel model,
            long offset = 0
        )
        {
            var block = new long[5];
            VectorNode best = null;
            float highscore = 0;

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

        private Hit ClosestMatchInPage(
            Vector vector,
            Memory<VectorNodeData> indexMemory,
            MemoryMappedViewAccessor vectorView,
            IStringModel model
        )
        {
            int offset = 0;
            Span<VectorNodeData> page = indexMemory.Span;
            VectorNode best = null;
            float highscore = 0;

            while (true)
            {
                var data = page[offset++];
                var cursorVector = model.DeserializeVector(data.VectorOffset, (int)data.ComponentCount, vectorView);
                var angle = model.CosAngle(cursorVector, vector);

                if (angle >= model.IdenticalAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector, new List<long> { data.PostingsOffset });
                    }
                    else if (angle == highscore)
                    {
                        best.PostingsOffsets.Add(data.PostingsOffset);
                    }

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (best == null || angle > highscore)
                    {
                        highscore = angle;
                        best = new VectorNode(cursorVector, new List<long> { data.PostingsOffset });
                    }
                    else if (angle == highscore)
                    {
                        best.PostingsOffsets.Add(data.PostingsOffset);
                    }

                    // We need to determine if we can traverse further left.
                    bool canGoLeft = data.Terminator == 0 || data.Terminator == 1;

                    if (!canGoLeft)
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
                        best = new VectorNode(cursorVector, new List<long> { data.PostingsOffset });
                    }
                    else if (angle == highscore)
                    {
                        best.PostingsOffsets.Add(data.PostingsOffset);
                    }

                    // We need to determine if we can traverse further to the right.

                    if (data.Terminator == 0)
                    {
                        // There exists a left and a right child.
                        // Next node in bitmap is the left child. 
                        // To find cursor's right child we must skip over the left tree.

                        var weight = (int)page[offset++].Weight;

                        offset += weight;
                    }
                    else if (data.Terminator != 2)
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

            var positionInBuffer = VectorNode.BlockSize - (sizeof(long) + sizeof(long));
            var weight = BitConverter.ToInt64(buf.Slice(positionInBuffer, sizeof(long)));
            var distance = weight * VectorNode.BlockSize;

            if (distance > 0)
            {
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }

        private void SkipTree(MemoryMappedViewAccessor indexView, ref long offset)
        {
            var weight = indexView.ReadInt64(offset + sizeof(long) + sizeof(long) + sizeof(long));

            offset += VectorNode.BlockSize;

            var distance = weight * VectorNode.BlockSize;

            offset += distance;
        }

        private void SkipTree(Memory<long> indexMemory, ref int offset)
        {
            var weight = (int)indexMemory.Span[offset + 3];

            offset += 5 + (weight * 5);
        }
    }
}
