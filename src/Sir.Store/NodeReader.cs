using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
        private readonly object _syncRefresh = new object();
        private VectorNode _root;
        private int _skip;
        private bool _refreshing;

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

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(_ixpFileName);
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = Path.GetFileName(_ixpFileName);
            watcher.Changed += new FileSystemEventHandler(OnFileChanged);
            watcher.EnableRaisingEvents = true;
        }

        public void Add(VectorNode node)
        {
            _root.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
        }

        public void Optimize()
        {
            _root = Optimized();
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_refreshing) return;

            _refreshing = true;

            var pages = _sessionFactory.ReadPageInfoFromDisk(_ixpFileName).ToList();

            foreach (var page in pages.Skip(_skip))
            {
                var time = Stopwatch.StartNew();

                using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
                {
                    ixStream.Seek(page.offset, SeekOrigin.Begin);

                    VectorNode.DeserializeTree(ixStream, vectorStream, page.length, _root);

                    this.Log($"refreshed page {page.offset} in {time.Elapsed}");
                }
            }

            _skip = pages.Count;
            _refreshing = false;
        }

        public VectorNode Optimized()
        {
            var optimized = new VectorNode();
            var pages = _sessionFactory.ReadPageInfoFromDisk(_ixpFileName).ToList();

            Parallel.ForEach(pages.Skip(_skip), page =>
            {
                var time = Stopwatch.StartNew();

                using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
                {
                    ixStream.Seek(page.offset, SeekOrigin.Begin);

                    VectorNode.DeserializeTree(ixStream, vectorStream, page.length, optimized);

                    this.Log($"optimized page {page.offset} in {time.Elapsed}");
                }
            });

            _skip = pages.Count;

            return optimized;
        }

        public Hit ClosestMatch(SortedList<long, byte> vector)
        {
            Hit high = _root.ClosestMatch(vector, VectorNode.TermFoldAngle);

            return high;

            //var time = Stopwatch.StartNew();

            //using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
            //{
            //    var hit = ClosestMatchInPage(
            //                vector,
            //                indexStream,
            //                vectorStream,
            //                new Queue<(long offset, long length)>(pages));

            //    if (high == null || hit.Score > high.Score)
            //    {
            //        high = hit;
            //    }
            //    else if (high != null && hit.Score == high.Score)
            //    {
            //        high.Node.Merge(hit.Node);
            //    }
            //}

            //this.Log($"cache miss. scan took {time.Elapsed}");

            //return high;
        }

        private Hit ClosestMatchInPage(
            SortedList<long, byte> node, 
            Stream indexStream, 
            Stream vectorStream,
            Queue<(long offset, long length)> pages)
        {
            pages.Dequeue();

            var cursor = ReadNode(indexStream, vectorStream);

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
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // we need to determine if we can traverse further left
                    bool canGoLeft = cursor.Terminator == 0 || cursor.Terminator == 1;

                    if (canGoLeft)
                    {
                        // there is a left and a right child or simply a left child
                        // either way, next node in bitmap is the left child

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        // there is no left child.

                        if (pages.Count == 0) break;

                        indexStream.Seek(pages.Dequeue().offset, SeekOrigin.Begin);

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                }
                else
                {
                    if (angle > highscore)
                    {
                        highscore = angle;
                        best = cursor;
                    }
                    else if (angle > 0 && angle == highscore)
                    {
                        if (best.PostingsOffsets == null)
                        {
                            best.PostingsOffsets = new List<long> { best.PostingsOffset, cursor.PostingsOffset };
                        }
                        else
                        {
                            best.PostingsOffsets.Add(cursor.PostingsOffset);
                        }
                    }

                    // we need to determine if we can traverse further to the right

                    if (cursor.Terminator == 0)
                    {
                        // there is a left and a right child
                        // next node in bitmap is the left child 
                        // to find cursor's right child we must skip over the left tree

                        SkipTree(indexStream);

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else if (cursor.Terminator == 2)
                    {
                        // next node in bitmap is the right child

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                    else
                    {
                        if (pages.Count == 0) break;

                        indexStream.Seek(pages.Dequeue().offset, SeekOrigin.Begin);

                        cursor = ReadNode(indexStream, vectorStream);
                    }
                }
            }

            //_root.Add(best, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);

            return new Hit
            {
                Score = highscore,
                Node = best
            };
        }

        private VectorNode ReadNode(Stream indexStream, Stream vectorStream)
        {
            var buf = new byte[VectorNode.NodeSize];
            var read = indexStream.Read(buf);

            if (read == 0) return null;

            var terminator = buf[buf.Length - 1];
            var node = VectorNode.DeserializeNode(buf, vectorStream, ref terminator);

            return node;
        }

        private void SkipTree(Stream indexStream)
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
                indexStream.Seek(distance, SeekOrigin.Current);
            }
        }
    }
}
