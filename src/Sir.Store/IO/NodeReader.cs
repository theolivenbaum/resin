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
    public class NodeReader : ILogger
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly string _ixpFileName;
        private readonly string _ixMapName;
        private readonly string _ixFileName;
        private readonly string _vecFileName;
        private long _optimizedOffset;

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

        public Hit ClosestMatch(SortedList<long, int> vector, (float identicalAngle, float foldAngle) similarity)
        {
            var hits = ClosestMatchOnDisk(vector, similarity);
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

        private ConcurrentBag<Hit> ClosestMatchOnDisk(SortedList<long, int> vector, (float identicalAngle, float foldAngle) similarity)
        {
            var time = Stopwatch.StartNew();
            var pages = _sessionFactory.ReadPageInfoFromDisk(_ixpFileName);
            var hits = new ConcurrentBag<Hit>();
            var ixbufferSize = int.Parse(_config.Get("index_read_buffer_size") ?? "4096");

            //foreach(var page in pages)
            Parallel.ForEach(pages, page =>
            {
                using (var indexStream = new BufferedStream(_sessionFactory.CreateReadStream(_ixFileName), ixbufferSize))
                using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                {
                    indexStream.Seek(page.offset, SeekOrigin.Begin);

                    var hit = ClosestMatchInPage(
                                vector,
                                indexStream,
                                vectorStream,
                                similarity);

                    hits.Add(hit);
                }
            });

            this.Log($"scan took {time.Elapsed}");

            return hits;
        }

        private Hit ClosestMatchInPage(
            SortedList<long, int> node,
            Stream indexStream,
            Stream vectorStream,
            (float identicalAngle, float foldAngle) similarity
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
                var cursorVector = VectorOperations.DeserializeVector(vecOffset, componentCount, vectorStream);
                var cursorTerminator = block[block.Length - 1];
                var postingsOffset = BitConverter.ToInt64(block.Slice(sizeof(long), sizeof(long)));

                var angle = cursorVector.CosAngle(node);

                if (angle >= similarity.identicalAngle)
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
                else if (angle > similarity.foldAngle)
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
    }
}
