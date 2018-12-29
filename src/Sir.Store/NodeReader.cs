using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class NodeReader : IDisposable
    {
        private readonly IList<(long offset, long length)> _pages;
        private readonly string _ixFileName;
        private readonly string _vecFileName;

        public NodeReader(string ixFileName, string vecFileName, Stream pageIndexStream)
        {
            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _pages = new PageIndexReader(pageIndexStream).ReadAll();

            pageIndexStream.Dispose();
        }

        public IList<Hit> ClosestMatch(SortedList<int, byte> node)
        {
            var toplist = new ConcurrentBag<Hit>();

            Parallel.ForEach(_pages, page =>
            {
                using (var indexStream = new FileStream(_ixFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
                using (var vectorStream = new FileStream(_vecFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
                {
                    if (indexStream.Position < page.offset)
                    {
                        indexStream.Seek(page.offset, SeekOrigin.Begin);
                    }

                    var hit = ClosestMatchInPage(node, indexStream, page.offset + page.length, vectorStream);

                    if (hit.Score > 0)
                    {
                        toplist.Add(hit);
                    }
                }
            });

            return new List<Hit>(toplist);
        }

        private Hit ClosestMatchInPage(SortedList<int, byte> node, Stream indexStream, long endOfSegment, Stream vectorStream)
        {
            var cursor = ReadNode(indexStream, vectorStream, endOfSegment);

            if (cursor == null)
            {
                throw new InvalidOperationException();
            }

            var best = cursor;
            float highscore = 0;

            while (cursor != null)
            {
                var angle = cursor.TermVector.CosAngle(node);

                if (angle > VectorNode.FoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }

                    // we need to determine if we can traverse further left
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft) 
                    {
                        // there is a left and a right child or simply a left child
                        // either way, next node in bitmap is the left child

                        cursor = ReadNode(indexStream, vectorStream, endOfSegment);
                    }
                    else 
                    {
                        // there is no left child
                        break;
                    }
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }

                    // we need to determine if we can traverse further to the right

                    if (cursor.Terminator == 0) 
                    {
                        // there is a left and a right child
                        // next node in bitmap is the left child 
                        // to find cursor's right child we must skip over the left tree

                        SkipTree(indexStream, endOfSegment);

                        cursor = ReadNode(indexStream, vectorStream, endOfSegment);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // next node in bitmap is the right child

                        cursor = ReadNode(indexStream, vectorStream, endOfSegment);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return new Hit
            {
                Embedding = best.TermVector,
                Score = highscore,
                PostingsOffset = best.PostingsOffset
            };
        }

        private VectorNode ReadNode(Stream indexStream, Stream vectorStream, long endOfSegment)
        {
            if (indexStream.Position + VectorNode.NodeSize >= endOfSegment)
            {
                return null;
            }

            var buf = new byte[VectorNode.NodeSize];
            var read = indexStream.Read(buf);
            var terminator = buf[buf.Length - 1];

            return VectorNode.DeserializeNode(buf, vectorStream, ref terminator);
        }

        private void SkipTree(Stream indexStream, long endOfSegment)
        {
            var buf = new byte[VectorNode.NodeSize];

            var read = indexStream.Read(buf);

            if (read == 0)
            {
                throw new InvalidOperationException();
            }

            var positionInBuffer = VectorNode.NodeSize - (sizeof(int) + sizeof(byte));
            var weight = BitConverter.ToInt32(buf, positionInBuffer);
            var distance = weight * VectorNode.NodeSize;

            if (distance > 0)
            {
                if (endOfSegment - (indexStream.Position + distance) > 0)
                {
                    indexStream.Seek(distance, SeekOrigin.Current);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private async Task<SortedList<int, byte>> ReadEmbedding(long offset, int numOfComponents, Stream vectorStream)
        {
            var vec = new SortedList<int, byte>();
            var vecBuf = new byte[numOfComponents * VectorNode.ComponentSize];

            vectorStream.Seek(offset, SeekOrigin.Begin);

            await vectorStream.ReadAsync(vecBuf);

            var offs = 0;

            for (int i = 0; i < numOfComponents; i++)
            {
                var key = BitConverter.ToInt32(vecBuf, offs);
                var val = vecBuf[offs + sizeof(int)];

                vec.Add(key, val);

                offs += VectorNode.ComponentSize;
            }

            return vec;
        }

        public void Dispose()
        {
        }
    }
}
