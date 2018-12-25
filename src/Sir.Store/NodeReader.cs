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
        private readonly byte[] _buf;
        private readonly IList<(long offset, long length)> _pages;

        public NodeReader(Stream indexStream, Stream vectorStream, Stream pageIndexStream)
        {
            _indexStream = indexStream;
            _vectorStream = vectorStream;
            _buf = new byte[VectorNode.NodeSize];
            _pages = new PageIndexReader(pageIndexStream).ReadAll();
            pageIndexStream.Dispose();
        }

        private async Task<byte[]> ReadNode()
        {
            var read = await _indexStream.ReadAsync(_buf);

            if (read != _buf.Length)
            {
                return null;
            }

            return _buf;
        }

        private async Task SkipTree()
        {
            var x = 1;
            byte terminator;

            while (x > 0)
            {
                var read = await _indexStream.ReadAsync(_buf);

                x--;

                if (read != _buf.Length)
                {
                    throw new InvalidOperationException("can't do that at the end of the file");
                }

                terminator = _buf[_buf.Length - 1];

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

        public async Task<IList<Hit>> ClosestMatch(SortedList<int, byte> node)
        {
            var toplist = new List<Hit>();
            Hit best = null;
            var term = new VectorNode(node);

            foreach (var page in _pages)
            {
                //if (_indexStream.Position > page.offset)
                //{
                //    throw new DataMisalignedException();
                //}
                //else if (_indexStream.Position < page.offset)
                //{
                //    _indexStream.Seek(page.offset, SeekOrigin.Begin);
                //}

                //var hit = await ClosestMatchInPage(node);

                var tree = await VectorNode.Deserialize(_indexStream, _vectorStream, page.offset, page.length);

                var hit = tree.ClosestMatch(term);

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
            var cursor = await ReadNode();
            var best = new byte[VectorNode.NodeSize];
            Buffer.BlockCopy(cursor, 0, best, 0, VectorNode.NodeSize);
            float highscore = 0;
            SortedList<int, byte> bestEmbedding = null;

            while (cursor != null)
            {
                var vecOffset = BitConverter.ToInt64(cursor, sizeof(float));
                var componentCount = BitConverter.ToInt32(cursor, sizeof(float) + sizeof(long) + sizeof(long));
                var embedding = await ReadEmbedding(vecOffset, componentCount);
                var terminator = cursor[cursor.Length - 1];
                var angle = embedding.CosAngle(node);

                if (angle > VectorNode.FoldAngle)
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        Buffer.BlockCopy(cursor, 0, best, 0, VectorNode.NodeSize);
                        bestEmbedding = embedding;
                    }

                    // we need to determine if we can traverse further left
                    bool canGoLeft = terminator == 0 || terminator == 1;

                    if (canGoLeft) 
                    {
                        // there is a left and a right child or simply a left child
                        // either way, next node in bitmap is the left child
                        cursor = await ReadNode();
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
                        Buffer.BlockCopy(cursor, 0, best, 0, VectorNode.NodeSize);
                        bestEmbedding = embedding;
                    }

                    // we need to determine if we can traverse further to the right

                    if (terminator == 0) 
                    {
                        // there is a left and a right child
                        // next node in bitmap is the left child 
                        // to find cursor's right child we must skip over the left tree
                        await SkipTree();

                        cursor = await ReadNode();
                    }
                    else if (terminator == 2)
                    {
                        // next node in bitmap is the right child
                        cursor = await ReadNode();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return new Hit
            {
                Embedding = bestEmbedding,
                Score = highscore,
                PostingsOffset = BitConverter.ToInt64(best, sizeof(float) + sizeof(long))
            };
        }

        public void Dispose()
        {
            _indexStream.Dispose();
            _vectorStream.Dispose();
        }
    }
}
