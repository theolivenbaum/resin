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
        private readonly Stream _indexStream;
        private readonly string _ixFileName;

        public NodeReader(
            ulong collectionId,
            long keyId,
            SessionFactory sessionFactory,
            IConfigurationProvider config)
        {
            var ixFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            var ixpFileName = Path.Combine(sessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));

            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _indexStream = _sessionFactory.CreateReadStream(_ixFileName, bufferSize: int.Parse(_config.Get("nodereader_buffer_size")), fileOptions: FileOptions.RandomAccess);
        }

        public Hit ClosestMatch(IVector vector, IStringModel model, Stream vectorStream)
        {
            var hits = ClosestMatchOnDisk(vector, model, vectorStream);
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

        private IEnumerable<Hit> ClosestMatchOnDisk(
            IVector vector, IStringModel model, Stream vectorStream)
        {
            var time = Stopwatch.StartNew();
            var pages = _sessionFactory.ReadPageInfo(_ixpFileName);
            var hits = new List<Hit>();

            foreach (var page in pages)
            {
                _indexStream.Seek(page.offset, SeekOrigin.Begin);

                var hit = ClosestMatchInPage(
                vector,
                _indexStream,
                vectorStream,
                model);

                hits.Add(hit);
            }

            this.Log($"scan of {pages.Count} pages took {time.Elapsed}");

            return hits;
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
                var cursorVector = model.DeserializeVector(vecOffset, (int)componentCount, vectorStream);
                var cursorTerminator = BitConverter.ToInt64(block.Slice(sizeof(long) * 4));
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
                        best.PostingsOffsets.Add(postingsOffset);
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
                        best = new VectorNode(cursorVector);
                        best.PostingsOffsets = new List<long> { postingsOffset };
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
            if (_indexStream != null)
                _indexStream.Dispose();
        }
    }
}
