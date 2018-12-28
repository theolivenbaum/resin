using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class NodeReader : IDisposable
    {
        private readonly Stream _indexStream;
        private readonly Stream _vectorStream;
        private readonly IList<(long offset, long length)> _pages;
        private byte[] _buf;

        public NodeReader(Stream indexStream, Stream vectorStream, Stream pageIndexStream)
        {
            _indexStream = indexStream;
            _vectorStream = vectorStream;
            _buf = new byte[VectorNode.NodeSize];

            _pages = new PageIndexReader(pageIndexStream).ReadAll();
            pageIndexStream.Dispose();
        }

        private VectorNode ReadNode(long endOfSegment)
        {
            if (_indexStream.Position + VectorNode.NodeSize >= endOfSegment)
            {
                return null;
            }

            var read = _indexStream.Read(_buf);
            var terminator = _buf[_buf.Length - 1];

            return VectorNode.DeserializeNode(_buf, _vectorStream, ref terminator);
        }

        private void SkipTree(byte terminator, long endOfSegment)
        {
            var skipsNeeded = 0;

            if (terminator == 0)
            {
                skipsNeeded = 2;
            }
            else if (terminator < 3)
            {
                skipsNeeded = 1;
            }

            var buf = new byte[VectorNode.NodeSize];

            while (skipsNeeded > 0 && _indexStream.Position + VectorNode.NodeSize > endOfSegment)
            {
                var read = _indexStream.Read(buf);

                skipsNeeded--;

                if (read != buf.Length)
                {
                    throw new InvalidOperationException("can't do that at the end of the file");
                }

                terminator = buf[buf.Length - 1];

                if (terminator == 0)
                {
                    skipsNeeded += 2;
                }
                else if (terminator < 3)
                {
                    skipsNeeded++;
                }
            }
        }

        private async Task<SortedList<int, byte>> ReadEmbedding(long offset, int numOfComponents)
        {
            var vec = new SortedList<int, byte>();
            var vecBuf = new byte[numOfComponents * VectorNode.ComponentSize];

            _vectorStream.Seek(offset, SeekOrigin.Begin);

            await _vectorStream.ReadAsync(vecBuf);

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

        public IList<Hit> ClosestMatch(SortedList<int, byte> node)
        {
            var toplist = new List<Hit>();
            Hit best = null;
            var term = new VectorNode(node);

            foreach (var page in _pages)
            {
                if (_indexStream.Position < page.offset)
                {
                    _indexStream.Seek(page.offset, SeekOrigin.Begin);
                }

                var hit = ClosestMatchInPage(node, page.offset + page.length);
                //var hit = VectorNode.ScanTree(term, _indexStream, _vectorStream, page.length);
                //var hit = VectorNode.DeserializeTree(_indexStream, _vectorStream, page.length).ClosestMatch(term);

                if (hit.Score > 0)
                {
                    if (best == null)
                    {
                        best = hit;
                        toplist.Add(best);
                    }
                    else if (hit.Score > best.Score)
                    {
                        best = hit;
                        toplist.Insert(0, hit);
                    }
                    else if (hit.Score == best.Score)
                    {
                        toplist.Add(hit);
                    }
                }
            }

            return toplist;
        }

        private Hit ClosestMatchInPage(SortedList<int, byte> node, long endOfSegment)
        {
            var cursor = ReadNode(endOfSegment);

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

                        cursor = ReadNode(endOfSegment);
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

                        SkipTree(cursor.Terminator, endOfSegment);

                        cursor = ReadNode(endOfSegment);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // next node in bitmap is the right child

                        cursor = ReadNode(endOfSegment);
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

        public void Dispose()
        {
            _indexStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}
