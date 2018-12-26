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
        private byte _terminator = 2;
        private byte[] _buf;

        public NodeReader(Stream indexStream, Stream vectorStream, Stream pageIndexStream)
        {
            _indexStream = indexStream;
            _vectorStream = vectorStream;
            _buf = new byte[VectorNode.NodeSize];

            _pages = new PageIndexReader(pageIndexStream).ReadAll();
            pageIndexStream.Dispose();
        }

        private VectorNode ReadNode()
        {
            var read = _indexStream.Read(_buf);

            if (read != _buf.Length)
            {
                return null;
            }

            return VectorNode.DeserializeNode(_buf, _vectorStream, ref _terminator);
        }

        private async Task SkipTree()
        {
            var x = 1;
            var buf = new byte[VectorNode.NodeSize];
            byte terminator = buf[buf.Length - 1];

            while (x > 0)
            {
                var read = await _indexStream.ReadAsync(buf);

                x--;

                if (read != buf.Length)
                {
                    throw new InvalidOperationException("can't do that at the end of the file");
                }

                if (terminator == 0)
                {
                    x += 2;
                }
                else if (terminator == 1 || terminator == 2)
                {
                    x++;
                }
                else if (terminator == 3)
                {
                    x--;
                }

                terminator = buf[buf.Length - 1];
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
                if (_indexStream.Position > page.offset)
                {
                    throw new DataMisalignedException();
                }
                else if (_indexStream.Position < page.offset)
                {
                    _indexStream.Seek(page.offset, SeekOrigin.Begin);
                }

                var hit = VectorNode.ScanTree(term, _indexStream, _vectorStream, page.length);
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

        private async Task<Hit> ClosestMatchInPage(SortedList<int, byte> node)
        {
            var cursor = ReadNode();
            var best = cursor.Clone();
            float highscore = 0;

            while (cursor != null)
            {
                var angle = cursor.TermVector.CosAngle(node);

                if (angle > VectorNode.FoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor.Clone();
                    }

                    // we need to determine if we can traverse further left
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft) 
                    {
                        // there is a left and a right child or simply a left child
                        // either way, next node in bitmap is the left child

                        cursor = ReadNode();
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
                        best = cursor.Clone();
                    }

                    // we need to determine if we can traverse further to the right

                    if (cursor.Terminator == 0) 
                    {
                        // there is a left and a right child
                        // next node in bitmap is the left child 
                        // to find cursor's right child we must skip over the left tree

                        await SkipTree();

                        cursor = ReadNode();
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // next node in bitmap is the right child

                        cursor = ReadNode();
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
