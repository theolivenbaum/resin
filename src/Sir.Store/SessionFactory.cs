using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Owner and manager of in-memory index. Dispatcher of sessions.
    /// </summary>
    public class SessionFactory
    {
        private readonly ITokenizer _tokenizer;
        private readonly IConfigurationService _config;
        private readonly ConcurrentDictionary<ulong, long> _keys;
        private VectorTree _index;
        private readonly StreamWriter _log;
        private readonly object _sync = new object();

        private Stream _writableKeyMapStream { get; }

        public string Dir { get; }

        public SessionFactory(string dir, ITokenizer tokenizer, IConfigurationService config)
        {
            Dir = dir;
            _log = Logging.CreateWriter("sessionfactory");

            var tasks = new Task[1];
            tasks[0] = LoadIndex();

            _keys = LoadKeyMap();
            _tokenizer = tokenizer;
            _config = config;

            _writableKeyMapStream = new FileStream(
                Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            Task.WaitAll(tasks);
        }

        public async Task Publish(string collection, RemotePostingsWriter postings)
        {
            var timer = new Stopwatch();
            timer.Start();

            var collectionId = collection.ToHash();
            var ix = GetCollectionIndex(collectionId);

            foreach (var x in ix)
            {
                var t1 = new Stopwatch();
                t1.Start();

                await postings.Write(collection, x.Value);

                _log.Log(string.Format("wrote postings in {0}", t1.Elapsed));

                var t2 = new Stopwatch();
                t2.Start();

                using (var ixStream = CreateIndexStream(collectionId, x.Key))
                {
                    await x.Value.SerializeTree(ixStream);
                }

                _log.Log(string.Format("wrote tree in {0}", t2.Elapsed));
            }

            _log.Log(string.Format("published index in {0}", timer.Elapsed));
        }

        private Stream CreateIndexStream(ulong collectionId, long keyId)
        {
            var fileName = Path.Combine(Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            return new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        }

        private ConcurrentDictionary<ulong, long> LoadKeyMap()
        {
            var timer = new Stopwatch();
            timer.Start();

            var keys = new ConcurrentDictionary<ulong, long>();

            using (var stream = new FileStream(
                Path.Combine(Dir, "_.kmap"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
            {
                long i = 0;
                var buf = new byte[sizeof(ulong)];
                var read = stream.Read(buf, 0, buf.Length);

                while (read > 0)
                {
                    keys.GetOrAdd(BitConverter.ToUInt64(buf, 0), i++);

                    read = stream.Read(buf, 0, buf.Length);
                }
            }

            _log.Log("loaded keys in {0}", timer.Elapsed);

            return keys;
        }

        private async Task LoadIndex()
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                _log.Log("begin loading index into memory");

                var ixs = new ConcurrentDictionary<ulong, ConcurrentDictionary<long, VectorNode>>();
                var indexFiles = Directory.GetFiles(Dir, "*.ix");

                foreach (var ixFileName in indexFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(ixFileName)
                        .Split(".", StringSplitOptions.RemoveEmptyEntries);

                    var collectionHash = ulong.Parse(name[0]);
                    var keyId = long.Parse(name[1]);
                    var vecFileName = Path.Combine(Dir, string.Format("{0}.{1}.vec", collectionHash, keyId));

                    ConcurrentDictionary<long, VectorNode> colIx;

                    if (!ixs.TryGetValue(collectionHash, out colIx))
                    {
                        colIx = new ConcurrentDictionary<long, VectorNode>();
                        ixs.GetOrAdd(collectionHash, colIx);
                    }

                    var ix = await DeserializeIndex(ixFileName, vecFileName);
                    colIx.GetOrAdd(keyId, ix);

                    _log.Log(string.Format("loaded {0}.{1}. {2}",
                        collectionHash, keyId, ix.Size()));
                }

                _index = new VectorTree(ixs);

                if (indexFiles.Length == 0)
                {
                    _log.Log("found no index files in {0}. index is empty.", Dir);
                }
                else
                {
                    _log.Log("deserialized {0} index files in {1}", indexFiles.Length, timer.Elapsed);

                    // validate
                    foreach (var validateFn in Directory.GetFiles(Dir, "*.validate"))
                    {
                        _log.Log("validating {0}", validateFn);

                        var fi = new FileInfo(validateFn);
                        var segs = Path.GetFileNameWithoutExtension(fi.Name).Split('.');
                        var col = ulong.Parse(segs[0]);
                        var key = long.Parse(segs[1]);
                        var colIx = ixs[col];
                        var ix = colIx[key];

                        foreach (var token in File.ReadAllLines(validateFn))
                        {
                            var closestMatch = ix.ClosestMatch(new VectorNode(token), skipDirtyNodes: false);

                            if (closestMatch.Score < VectorNode.IdenticalAngle)
                            {
                                throw new DataMisalignedException();
                            }
                            else
                            {
                                File.Delete(validateFn);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private async Task<VectorNode> DeserializeIndex(string ixFileName, string vecFileName)
        {
            using (var treeStream = new FileStream(ixFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
            using (var vecStream = new FileStream(vecFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
            {
                return await VectorNode.Deserialize(treeStream, vecStream);
            }
        }

        public async Task PersistKeyMapping(ulong keyHash, long keyId)
        {
            lock (_sync)
            {
                _keys.GetOrAdd(keyHash, keyId);
            }

            var buf = BitConverter.GetBytes(keyHash);

            await _writableKeyMapStream.WriteAsync(buf, 0, sizeof(ulong));

            await _writableKeyMapStream.FlushAsync();
        }

        public long GetKeyId(ulong keyHash)
        {
            return _keys[keyHash];
        }

        public bool TryGetKeyId(ulong keyHash, out long keyId)
        {
            if (!_keys.TryGetValue(keyHash, out keyId))
            {
                keyId = -1;
                return false;
            }
            return true;
        }

        public ConcurrentDictionary<long, VectorNode> GetCollectionIndex(ulong collectionId)
        {
            var ix = _index.GetIndex(collectionId);

            if (ix == null)
            {
                lock (_sync)
                {
                    ix = _index.GetIndex(collectionId);

                    if (ix == null)
                    {
                        ix = new ConcurrentDictionary<long, VectorNode>();
                    }
                }
            }

            return ix;
        }

        public ConcurrentDictionary<long, VectorNode> GetOrAddCollectionIndex(ulong collectionId)
        {
            var ix = _index.GetIndex(collectionId);

            if (ix == null)
            {
                lock (_sync)
                {
                    ix = _index.GetIndex(collectionId);

                    if (ix == null)
                    {
                        ix = new ConcurrentDictionary<long, VectorNode>();
                        _index.Add(collectionId, ix);
                    }
                }
            }

            return ix;
        }

        public WriteSession CreateWriteSession(string collectionId)
        {
            return new WriteSession(collectionId, this);
        }

        public IndexingSession CreateIndexSession(string collectionId)
        {
            return new IndexingSession(collectionId, this, _tokenizer, _config);
        }

        public ReadSession CreateReadSession(string collectionId)
        {
            return new ReadSession(collectionId, this, _config);
        }

        public Stream CreateReadWriteStream(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public Stream CreateAppendStream(string fileName)
        {
            // https://stackoverflow.com/questions/122362/how-to-empty-flush-windows-read-disk-cache-in-c
            //const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
            //FileStream file = new FileStream(fileName, fileMode, fileAccess, fileShare, blockSize,
            //    FileFlagNoBuffering | FileOptions.WriteThrough | fileOptions);

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
        }
    }
}
