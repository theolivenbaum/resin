using RocksDbSharp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ConcurrentDictionary<string, MemoryMappedFile> _mmfs;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<string, IList<(long offset, long length)>> _pageInfo;
        private readonly ConcurrentDictionary<string, VectorNode> _graph;
        private readonly RocksDb _db;
        private static readonly object WriteSync = new object();
        private bool _isInitialized;

        public string Dir { get; }
        public IConfigurationProvider Config { get { return _config; } }

        public SessionFactory(IConfigurationProvider config, IStringModel model)
        {
            Dir = config.Get("data_dir");

            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            _model = model;
            _config = config;
            _pageInfo = new ConcurrentDictionary<string, IList<(long offset, long length)>>();
            _mmfs = new ConcurrentDictionary<string, MemoryMappedFile>();
            _graph = new ConcurrentDictionary<string, VectorNode>();

            var options = new DbOptions().SetCreateIfMissing(true);

            _db = RocksDb.Open(options, Path.Combine(Dir, "db"));
        }

        private void LoadGraph()
        {
            _isInitialized = false;

            var gtimer = Stopwatch.StartNew();

            Parallel.ForEach(Directory.GetFiles(Dir, "*.ix"), fileName =>
            //foreach (var fileName in Directory.GetFiles(Dir, "*.ix"))
            {
                var ftimer = Stopwatch.StartNew();
                var pageFileName = Path.Combine(Dir, $"{Path.GetFileNameWithoutExtension(fileName)}.ixp");
                var vectorFileName = Path.Combine(Dir, $"{Path.GetFileNameWithoutExtension(fileName)}.vec");
                var ixFile = OpenMMF(fileName);
                var vecFile = OpenMMF(vectorFileName);
                var root = _graph.GetOrAdd(fileName, new VectorNode());

                Parallel.ForEach(ReadPageInfo(pageFileName), page =>
                //foreach (var page in ReadPageInfo(pageFileName))
                {
                    var timer = Stopwatch.StartNew();

                    using (var vectorView = vecFile.CreateViewAccessor(0, 0))
                    using (var indexView = ixFile.CreateViewAccessor(page.offset, page.length))
                    {
                        try
                        {
                            var length = page.length / VectorNode.BlockSize;
                            var buf = new VectorNodeData[length];
                            var read = indexView.ReadArray(0, buf, 0, buf.Length);

                            foreach (var item in buf)
                            {
                                var vector = _model.DeserializeVector(
                                    item.VectorOffset, (int)item.ComponentCount, vectorView);

                                GraphBuilder.Add(
                                    root, new VectorNode(vector, new List<long> { item.PostingsOffset }), _model);
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Log(ex.ToString());

                            throw;
                        }
                    }

                    this.Log($"loaded page {page} from {fileName} into memory in {timer.Elapsed}");
                });

                this.Log($"{fileName} fully loaded into memory in {ftimer.Elapsed}");
            });

            this.Log($"graph fully loaded into memory in {gtimer.Elapsed}");

            _isInitialized = true;
        }

        private ConcurrentDictionary<string, ConcurrentDictionary<long, IMemoryOwner<VectorNodeData>>> LoadIndexMemory()
        {
            var indexMemory = new ConcurrentDictionary<string, ConcurrentDictionary<long, IMemoryOwner<VectorNodeData>>>();

            Parallel.ForEach(Directory.GetFiles(Dir, "*.ix"), fileName =>
            //foreach (var fileName in Directory.GetFiles(Dir, "*.ix"))
            {
                var pageFileName = Path.Combine(Dir, $"{Path.GetFileNameWithoutExtension(fileName)}.ixp");
                var indexFile = OpenMMF(fileName);
                var pages = indexMemory.GetOrAdd(fileName, new ConcurrentDictionary<long, IMemoryOwner<VectorNodeData>>());

                Parallel.ForEach(ReadPageInfo(pageFileName), page =>
                //foreach (var page in ReadPageInfo(pageFileName))
                {
                    var timer = Stopwatch.StartNew();

                    using (var indexView = indexFile.CreateViewAccessor(page.offset, page.length))
                    {
                        try
                        {
                            var length = page.length / VectorNode.BlockSize;
                            var buf = new VectorNodeData[length];
                            var read = indexView.ReadArray(0, buf, 0, buf.Length);
                            IMemoryOwner<VectorNodeData> owner = MemoryPool<VectorNodeData>.Shared.Rent(minBufferSize:buf.Length);
                            buf.AsSpan().CopyTo(owner.Memory.Span);
                            pages.GetOrAdd(page.offset, owner);
                        }
                        catch (Exception ex)
                        {
                            this.Log(ex.ToString());

                            throw;
                        }
                    }

                    this.Log($"loaded page {page} from {fileName} into memory in {timer.Elapsed}");
                });
            });

            return indexMemory;
        }

        public void BeginInit()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    LoadGraph();

                }
                catch (Exception ex)
                {
                    this.Log(ex.ToString());
                }
            }).Start();
        }

        public VectorNode GetGraph(string ixFileName)
        {
            if (!_isInitialized)
                throw new InvalidOperationException(
                    "Unable to respond while initializing. Check log to see progress of init.");

            return _graph[ixFileName];
        }

        public MemoryMappedFile OpenMMF(string fileName)
        {
            var mapName = fileName.Replace(":", "").Replace("\\", "_");

            return _mmfs.GetOrAdd(mapName, x =>
            {
                return MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, mapName, 0, MemoryMappedFileAccess.ReadWrite);
            });
        }

        public void Truncate(ulong collectionId)
        {
            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*"))
            {
                File.Delete(file);
            }

            _pageInfo.Clear();
        }

        public void Execute(Job job)
        {
            lock (WriteSync)
            {
                var timer = Stopwatch.StartNew();
                var colId = job.Collection.ToHash();

                using (var indexSession = CreateIndexSession(job.Collection, colId))
                using (var writeSession = CreateWriteSession(job.Collection, colId, indexSession))
                {
                    foreach (var doc in job.Documents)
                    {
                        writeSession.Write(doc);
                    }

                    writeSession.Commit();
                }

                _pageInfo.Clear();

                this.Log("executed {0} write+index job in {1}", job.Collection, timer.Elapsed);
            }
        }

        public IList<(long offset, long length)> ReadPageInfo(string pageFileName)
        {
            return _pageInfo.GetOrAdd(pageFileName, key =>
            {
                using (var ixpStream = CreateReadStream(key))
                {
                    return new PageIndexReader(ixpStream).ReadAll();
                }
            });
        }

        public WarmupSession CreateWarmupSession(string collectionName, ulong collectionId, string baseUrl)
        {
            return new WarmupSession(collectionName, collectionId, this, _model, _config, baseUrl);
        }

        public DocumentStreamSession CreateDocumentStreamSession(string collectionName, ulong collectionId)
        {
            return new DocumentStreamSession(collectionName, collectionId, this);
        }

        public WriteSession CreateWriteSession(string collectionName, ulong collectionId, TermIndexSession indexSession)
        {
            return new WriteSession(
                collectionName, collectionId, this, indexSession, _config, _db);
        }

        public TermIndexSession CreateIndexSession(string collectionName, ulong collectionId)
        {
            return new TermIndexSession(collectionName, collectionId, this, _model, _config);
        }

        public ValidateSession CreateValidateSession(string collectionName, ulong collectionId)
        {
            return new ValidateSession(
                collectionName, collectionId, this, _model, _config, CreateReadSession(collectionName, collectionId));
        }

        public ReadSession CreateReadSession(string collectionName, ulong collectionId)
        {
            return new ReadSession(collectionName, collectionId, this, _config, _model, _db);
        }

        public Stream CreateAsyncReadStream(string fileName)
        {
            return File.Exists(fileName)
            ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true)
            : null;
        }

        public Stream CreateReadStream(string fileName, FileOptions fileOptions = FileOptions.RandomAccess)
        {
            return File.Exists(fileName)
                ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, fileOptions)
                : null;
        }

        public Stream CreateAsyncAppendStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
        }

        public Stream CreateAppendStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public bool CollectionExists(ulong collectionId)
        {
            return File.Exists(Path.Combine(Dir, collectionId + ".pos"));
        }

        public void Dispose()
        {
            foreach(var x in _mmfs)
            {
                x.Value.Dispose();
            }
        }
    }
}