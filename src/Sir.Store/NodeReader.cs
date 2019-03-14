using System;
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
        private VectorNode _root;
        private readonly string _ixFileName;
        private readonly string _vecFileName;
        private readonly object _syncRefresh = new object();

        public NodeReader(
            string ixFileName, 
            string ixpFileName, 
            string vecFileName, 
            SessionFactory sessionFactory, 
            IConfigurationProvider config)
        {
            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");
            _root = new VectorNode();
        }

        private IList<(long offset, long length)> ReadPageInfoFromDisk()
        {
            using (var ixpStream = _sessionFactory.CreateReadStream(_ixpFileName))
            {
                return new PageIndexReader(ixpStream).ReadAll();
            }
        }

        public VectorNode Optimized()
        {
            var optimized = new VectorNode();

            Parallel.ForEach(ReadPageInfoFromDisk(), page =>
            {
                var time = Stopwatch.StartNew();

                using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
                {
                    ixStream.Seek(page.offset, SeekOrigin.Begin);

                    var tree = VectorNode.DeserializeTree(ixStream, vectorStream, page.length);

                    foreach (var node in tree.All())
                    {
                        optimized.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
                    }

                    this.Log($"added page {page.offset} to in-memory tree in {time.Elapsed}");
                }
            });

            return optimized;
        }

        private IEnumerable<Stream> AllPages()
        {
            var time = Stopwatch.StartNew();

            this.Log($"refreshing {_ixpFileName}");

            var ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");

            using (var ixmmf = _sessionFactory.CreateMMF(_ixFileName, ixMapName))
            {
                foreach (var page in ReadPageInfoFromDisk())
                {
                    using (var indexStream = ixmmf.CreateViewStream(page.offset, page.length, MemoryMappedFileAccess.Read))
                    {
                        yield return indexStream;
                    }
                }
            }

            this.Log($"refreshed index in {time.Elapsed}");
        }

        public Hit ClosestMatch(SortedList<long, byte> vector)
        {
            Hit high = _root.ClosestMatch(vector, VectorNode.TermFoldAngle);

            if (high.Score >= VectorNode.TermIdenticalAngle)
            {
                return high;
            }

            var time = Stopwatch.StartNew();

            using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            {
                foreach (var indexStream in AllPages())
                {
                    var hit = ClosestMatchInPage(vector, indexStream, indexStream.Length, vectorStream);

                    if (high == null || hit.Score > high.Score)
                    {
                        high = hit;
                    }
                    else if (hit.Score == high.Score)
                    {
                        foreach (var offs in hit.PostingsOffsets)
                        {
                            high.PostingsOffsets.Add(offs);
                        }
                    }
                }
            }

            this.Log($"closest match took {time.Elapsed}");

            return high;
        }

        private Hit ClosestMatchInPage(
            SortedList<long, byte> node, 
            Stream indexStream, 
            long endOfSegment, 
            Stream vectorStream)
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

            _root.Add(best, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);

            return new Hit
            {
                Embedding = best.Vector,
                Score = highscore,
                PostingsOffsets = best.PostingsOffsets ?? new List<long> { best.PostingsOffset }
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
    }
}
