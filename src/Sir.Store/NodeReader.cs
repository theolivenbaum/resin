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
        private readonly IList<(long offset, long length)> _pages;
        private readonly SessionFactory _sessionFactory;
        private readonly string _ixFileName;
        private readonly string _vecFileName;
        private IList<VectorNode> _pageCache;
        private readonly object _sync = new object();

        public NodeReader(string ixFileName, string vecFileName, SessionFactory sessionFactory, IList<(long, long)> pages)
        {
            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _pages = pages;
            _sessionFactory = sessionFactory;
        }

        public IList<VectorNode> ReadAllPages()
        {
            if (_pageCache != null)
            {
                return _pageCache;
            }

            IList<VectorNode> pages = new List<VectorNode>();

            lock (_sync)
            {
                if (_pageCache == null)
                {
                    var time = Stopwatch.StartNew();

                    using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                    using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
                    {
                        foreach (var page in _pages)
                        {
                            ixStream.Seek(page.offset, SeekOrigin.Begin);

                            var tree = VectorNode.DeserializeTree(ixStream, vectorStream, page.length);

                            pages.Add(tree);
                        }
                    }

                    this.Log("deserialized {0} index segments in {1}", pages.Count, time.Elapsed);

                    _pageCache = pages;
                }
                else
                {
                    pages = _pageCache;
                }
            }

            return pages;
        }

        public IList<Hit> ClosestMatch(SortedList<int, byte> vector)
        {
            var toplist = new ConcurrentBag<Hit>();
            var query = new VectorNode(vector);

            Parallel.ForEach(ReadAllPages(), page =>
            {
                var hit = page.ClosestMatch(query, VectorNode.TermFoldAngle);

                if (hit.Score > 0)
                {
                    toplist.Add(hit);
                }
            });

            //var toplist = new ConcurrentBag<Hit>();
            //var ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");

            //using (var ixmmf = _sessionFactory.CreateMMF(_ixFileName, ixMapName))
            //{
            //    Parallel.ForEach(_pages, page =>
            //    {
            //        using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            //        using (var indexStream = ixmmf.CreateViewStream(page.offset, page.length, MemoryMappedFileAccess.Read))
            //        {
            //            var hit = ClosestMatchInPage(vector, indexStream, page.offset + page.length, vectorStream);

            //            if (hit.Score > 0)
            //            {
            //                toplist.Add(hit);
            //            }
            //        }
            //    });
            //}

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
                var angle = cursor.Vector.CosAngle(node);

                if (angle > VectorNode.TermFoldAngle)
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
                Embedding = best.Vector,
                Score = highscore,
                PostingsOffset = best.PostingsOffset,
                NodeId = Convert.ToInt32(best.VectorOffset)
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

        private void SkipTreeWithoutSeek(Stream indexStream, long endOfSegment)
        {
            var skipsNeeded = 1;
            var buf = new byte[VectorNode.NodeSize];

            while (skipsNeeded > 0)
            {
                var read = indexStream.Read(buf);

                if (read == 0)
                {
                    throw new InvalidOperationException();
                }

                var terminator = buf[buf.Length - 1];

                if (terminator == 0)
                {
                    skipsNeeded += 2;
                }
                else if (terminator < 3)
                {
                    skipsNeeded++;
                }

                skipsNeeded--;
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
    }
}
