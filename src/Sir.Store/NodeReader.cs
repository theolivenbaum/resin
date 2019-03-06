using Sir.Core;
using System;
using System.Collections.Concurrent;
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
        private IList<(long offset, long length)> _pages;
        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly string _ixpFileName;
        private readonly string _ixFileName;
        private readonly string _vecFileName;
        private VectorNode _root;

        public NodeReader(string ixFileName, string ixpFileName, string vecFileName, SessionFactory sessionFactory, IConfigurationProvider config)
        {
            _vecFileName = vecFileName;
            _ixFileName = ixFileName;
            _sessionFactory = sessionFactory;
            _config = config;
            _ixpFileName = ixpFileName;
            _pages = ReadPageInfoFromDisk();

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Directory.GetCurrentDirectory();
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = Path.GetFileName(_ixpFileName);
            watcher.Changed += new FileSystemEventHandler(OnFileChanged);
            watcher.EnableRaisingEvents = true;
        }

        private IList<(long, long)> ReadPageInfoFromDisk()
        {
            using (var ixpStream = _sessionFactory.CreateAsyncReadStream(_ixpFileName))
            {
                return new PageIndexReader(ixpStream).ReadAll();
            }
        }

        private readonly object _reloadSync = new object();

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var newPages = ReadPageInfoFromDisk();

            if (newPages.Count != _pages.Count)
            {
                lock (_reloadSync)
                {
                    newPages = ReadPageInfoFromDisk();

                    if (newPages.Count != _pages.Count)
                    {
                        var skip = _pages.Count;

                        _pages = newPages;

                        AllPages(skip);
                    }
                }
            }
        }

        private readonly object _syncInit = new object();

        public VectorNode AllPages(int skip = 0)
        {
            if (_root != null)
            {
                return _root;
            }

            lock (_syncInit)
            {
                if (_root != null)
                {
                    return _root;
                }

                var time = Stopwatch.StartNew();

                _root = new VectorNode();

                using (var vectorStream = _sessionFactory.CreateReadStream(_vecFileName))
                using (var ixStream = _sessionFactory.CreateReadStream(_ixFileName))
                using (var queue = new ProducerConsumerQueue<VectorNode>(Build, int.Parse(_config.Get("write_thread_count"))))
                {
                    foreach (var (offset, length) in _pages.Skip(skip))
                    {
                        ixStream.Seek(offset, SeekOrigin.Begin);

                        var tree = VectorNode.DeserializeTree(ixStream, vectorStream, length);

                        queue.Enqueue(tree);
                    }
                }

                this.Log("deserialized {0} index segments in {1}", _pages.Count, time.Elapsed);

                return _root;
            }
        }

        private void Build(VectorNode tree)
        {
            foreach (var node in tree.All())
            {
                _root.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
            }

            this.Log("deserialized page");
        }

        public IEnumerable<Hit> Intersecting(SortedList<long, byte> vector)
        {
            var query = new VectorNode(vector);
            var tree = AllPages();
            var hits = tree.Intersecting(query, VectorNode.TermFoldAngle);

            return hits;
        }

        public Hit ClosestMatch(SortedList<long, byte> vector)
        {
            var query = new VectorNode(vector);
            var tree = AllPages();
            var hit = tree.ClosestMatch(query, VectorNode.TermFoldAngle);

            return hit;


            //var toplist = new ConcurrentBag<Hit>();

            //Parallel.ForEach(ReadAllPages(), page =>
            //{
            //    var hit = page.ClosestMatch(query, VectorNode.TermFoldAngle);

            //    if (hit.Score > 0)
            //    {
            //        toplist.Add(hit);
            //    }
            //});

            //var toplist = new List<Hit>();
            //var ixMapName = _ixFileName.Replace(":", "").Replace("\\", "_");

            //using (var ixmmf = _sessionFactory.CreateMMF(_ixFileName, ixMapName))
            //{
            //    foreach (var page in _pages)
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
            //    }
            //}

            //return toplist;
        }

        private Hit ClosestMatchInPage(SortedList<long, byte> node, Stream indexStream, long endOfSegment, Stream vectorStream)
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
