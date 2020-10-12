using Microsoft.Extensions.Logging;
using Sir.Document;
using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Sir.Search
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ISessionFactory
    {
        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> _keys;
        private readonly ConcurrentDictionary<string, IList<(long offset, long length)>> _pageInfo;
        private ILogger _logger;

        public string Dir { get; }
        public IConfigurationProvider Config { get; }

        public SessionFactory(IConfigurationProvider config = null, ILogger logger = null)
        {
            var time = Stopwatch.StartNew();

            Dir = config.Get("data_dir");
            Config = config ?? new KeyValueConfiguration();

            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            _pageInfo = new ConcurrentDictionary<string, IList<(long offset, long length)>>();
            _logger = logger;
            _keys = LoadKeys();

           Log($"loaded keys in {time.Elapsed}");
           Log($"sessionfactory is initiated.");
        }

        private void Log(string message)
        {
            if (_logger != null)
                _logger.LogInformation(message);
        }

        public long GetDocCount(string collection)
        {
            var fileName = Path.Combine(Dir, $"{collection.ToHash()}.dix");

            if (!File.Exists(fileName))
                return 0;

            return new FileInfo(fileName).Length / (sizeof(long) + sizeof(int));
        }

        public void Truncate(ulong collectionId)
        {
            var count = 0;

            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*"))
            {
                File.Delete(file);
                count++;
            }

            _pageInfo.Clear();

            _keys.Remove(collectionId, out _);

            Log($"truncated collection {collectionId} ({count} files)");
        }

        public void TruncateIndex(ulong collectionId)
        {
            var count = 0;

            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*.ix"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*.ixp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*.ixtp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*.vec"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(Dir, $"{collectionId}*.pos"))
            {
                File.Delete(file);
                count++;
            }

            Log($"truncated index {collectionId} ({count} files)");

            _pageInfo.Clear();
        }

        public void Optimize(
            string collection,
            HashSet<string> storeFields, 
            HashSet<string> indexFields,
            ITextModel model,
            int skip = 0,
            int take = 0,
            int batchSize = 1000000)
        {
            var collectionId = collection.ToHash();
            var totalCount = 0;

            TruncateIndex(collectionId);

            using (var docStream = new DocumentStreamSession(this))
            {
                foreach (var batch in docStream.ReadDocs(
                        collectionId,
                        storeFields,
                        skip,
                        take).Batch(batchSize))
                {
                    var job = new WriteJob(
                        collectionId,
                        batch,
                        model,
                        storeFields,
                        indexFields
                        );

                    Index(job, ref totalCount);

                    Log($"processed {totalCount} documents");
                }
            }

            Log($"optimized collection {collection}");
        }

        public void SaveAs(
            ulong targetCollectionId, 
            IEnumerable<IDictionary<string, object>> documents,
            HashSet<string> indexFieldNames,
            HashSet<string> storeFieldNames,
            ITextModel model,
            int reportSize = 1000)
        {
            var job = new WriteJob(targetCollectionId, documents, model, storeFieldNames, indexFieldNames);

            Write(job, reportSize);
        }

        public void Write(WriteJob job, WriteSession writeSession, IndexSession<string> indexSession, int reportSize = 1000)
        {
            Log($"writing to collection {job.CollectionId}");

            var time = Stopwatch.StartNew();

            var batchNo = 0;
            var count = 0;
            var batchTime = Stopwatch.StartNew();

            foreach (var document in job.Documents)
            {
                var docId = writeSession.Put(document, job.FieldNamesToStore);

                //Parallel.ForEach(document, kv =>
                foreach (var kv in document)
                {
                    if (job.FieldNamesToIndex.Contains(kv.Key) && kv.Value != null)
                    {
                        var keyId = writeSession.EnsureKeyExists(kv.Key);

                        indexSession.Put(docId, keyId, kv.Value.ToString());
                    }
                }//);

                if (count++ == reportSize)
                {
                    var info = indexSession.GetIndexInfo();
                    var t = batchTime.Elapsed.TotalSeconds;
                    var docsPerSecond = (int)(reportSize / t);
                    var debug = string.Join('\n', info.Info.Select(x => x.ToString()));

                    Log($"\n{time.Elapsed}\nbatch {++batchNo}\n{debug}\n{docsPerSecond} docs/s");

                    count = 0;
                    batchTime.Restart();
                }
            }

            _logger.LogInformation($"processed write job (collection {job.CollectionId}), time in total: {time.Elapsed}");
        }

        public void Write(
            IDictionary<string, object> document, 
            WriteSession writeSession, 
            IndexSession<string> indexSession,
            HashSet<string> fieldNamesToStore,
            HashSet<string> fieldNamesToIndex)
        {
            var docId = writeSession.Put(document, fieldNamesToStore);

            foreach (var kv in document)
            {
                if (fieldNamesToIndex.Contains(kv.Key) && kv.Value != null)
                {
                    var keyId = writeSession.EnsureKeyExists(kv.Key);

                    indexSession.Put(docId, keyId, kv.Value.ToString());
                }
            }
        }

        public void Index(WriteJob job, ref int totalCount, int reportSize = 1000)
        {
            Log($"indexing collection {job.CollectionId}");

            var time = Stopwatch.StartNew();
            var batchTime = Stopwatch.StartNew();
            var batchNo = 0;
            var batchCount = 0;

            using (var indexSession = CreateIndexSession(job.CollectionId, job.Model))
            {
                foreach (var document in job.Documents)
                {
                    var docId = (long)document[SystemFields.DocumentId];

                    foreach (var kv in document)
                    {
                        if (job.FieldNamesToIndex.Contains(kv.Key) && kv.Value != null)
                        {
                            var keyId = GetKeyId(job.CollectionId, kv.Key.ToHash());

                            indexSession.Put(docId, keyId, kv.Value.ToString());
                        }
                    }

                    if (batchCount++ == reportSize)
                    {
                        var info = indexSession.GetIndexInfo();
                        var t = batchTime.Elapsed.TotalMilliseconds;
                        var docsPerSecond = (int)(reportSize / t * 1000);
                        var debug = string.Join('\n', info.Info.Select(x => x.ToString()));

                        Log($"\n{time.Elapsed}\nbatch {++batchNo}\n{debug}\n{docsPerSecond} docs/s \ntotal {totalCount} docs");

                        batchTime.Restart();
                        totalCount += batchCount;
                        batchCount = 0;
                    }
                }
            }

            Log($"processed write job (collection {job.CollectionId}), time in total: {time.Elapsed}");
        }

        public void Write(WriteJob job, int reportSize = 1000)
        {
            using (var writeSession = CreateWriteSession(job.CollectionId))
            using (var indexSession = CreateIndexSession(job.CollectionId, job.Model))
            {
                Write(job, writeSession, indexSession, reportSize);
            }
        }

        public void Write(WriteJob job, IndexSession<string> indexSession, int reportSize)
        {
            using (var writeSession = CreateWriteSession(job.CollectionId))
            {
                Write(job, writeSession, indexSession, reportSize);
            }
        }

        public void Write(
            IEnumerable<IDictionary<string, object>> documents, 
            ITextModel model, 
            HashSet<string> storedFieldNames,
            HashSet<string> indexedFieldNames,
            int reportSize = 1000
            )
        {
            foreach (var group in documents.GroupBy(d => (string)d[SystemFields.CollectionId]))
            {
                var collectionId = group.Key.ToHash();

                using (var writeSession = CreateWriteSession(collectionId))
                using (var indexSession = CreateIndexSession(collectionId, model))
                {
                    Write(
                        new WriteJob(
                            collectionId, 
                            group, 
                            model, 
                            storedFieldNames, 
                            indexedFieldNames), 
                        writeSession, 
                        indexSession,
                        reportSize);
                }
            }
        }

        public FileStream CreateLockFile(ulong collectionId)
        {
            return new FileStream(Path.Combine(Dir, collectionId + ".lock"),
                   FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                   4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        }

        public void ClearPageInfo()
        {
            _pageInfo.Clear();
        }

        public IList<(long offset, long length)> GetAllPages(string pageFileName)
        {
            return _pageInfo.GetOrAdd(pageFileName, key =>
            {
                using (var ixpStream = CreateReadStream(key))
                {
                    return new PageIndexReader(ixpStream).GetAll();
                }
            });
        }

        public void Refresh()
        {
            _keys = LoadKeys();
        }

        public ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> LoadKeys()
        {
            var timer = Stopwatch.StartNew();
            var allkeys = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>>();

            foreach (var keyFile in Directory.GetFiles(Dir, "*.kmap"))
            {
                var collectionId = ulong.Parse(Path.GetFileNameWithoutExtension(keyFile));
                ConcurrentDictionary<ulong, long> keys;

                if (!allkeys.TryGetValue(collectionId, out keys))
                {
                    keys = new ConcurrentDictionary<ulong, long>();
                    allkeys.GetOrAdd(collectionId, keys);
                }

                using (var stream = new FileStream(keyFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
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
            }

            Log($"loaded keyHash -> keyId mappings into memory for {allkeys.Count} collections in {timer.Elapsed}");

            return allkeys;
        }

        public void RegisterKeyMapping(ulong collectionId, ulong keyHash, long keyId)
        {
            var fileName = Path.Combine(Dir, string.Format("{0}.kmap", collectionId));
            ConcurrentDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                keys = new ConcurrentDictionary<ulong, long>();
                _keys.GetOrAdd(collectionId, keys);
            }

            if (!keys.ContainsKey(keyHash))
            {
                keys.GetOrAdd(keyHash, keyId);

                using (var stream = CreateAppendStream(fileName))
                {
                    stream.Write(BitConverter.GetBytes(keyHash), 0, sizeof(ulong));
                }
            }
        }

        public long GetKeyId(ulong collectionId, ulong keyHash)
        {
            return _keys[collectionId][keyHash];
        }

        public bool TryGetKeyId(ulong collectionId, ulong keyHash, out long keyId)
        {
            var keys = _keys.GetOrAdd(collectionId, new ConcurrentDictionary<ulong, long>());

            if (!keys.TryGetValue(keyHash, out keyId))
            {
                keyId = -1;
                return false;
            }

            return true;
        }
        
        public DocumentStreamSession CreateDocumentStreamSession()
        {
            return new DocumentStreamSession(this);
        }

        public WriteSession CreateWriteSession(ulong collectionId)
        {
            var documentWriter = new DocumentWriter(collectionId, this);

            return new WriteSession(
                collectionId,
                documentWriter
            );
        }

        public IndexSession<string> CreateIndexSession(ulong collectionId, ITextModel model)
        {
            return new IndexSession<string>(collectionId, this, model, Config, _logger);
        }

        public IndexSession<IImage> CreateIndexSession(ulong collectionId, IImageModel model)
        {
            return new IndexSession<IImage>(collectionId, this, model, Config, _logger);
        }

        public IQuerySession CreateQuerySession(IModel model)
        {
            return new QuerySession(
                this,
                model,
                new PostingsReader(this),
                _logger);
        }

        public Stream CreateAsyncReadStream(string fileName, int bufferSize = 4096)
        {
            return File.Exists(fileName)
            ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous)
            : null;
        }

        public Stream CreateReadStream(string fileName, int bufferSize = 4096, FileOptions fileOptions = FileOptions.RandomAccess)
        {
            return File.Exists(fileName)
                ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, fileOptions)
                : null;
        }

        public Stream CreateAsyncAppendStream(string fileName, int bufferSize = 4096)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous);
        }

        public Stream CreateAppendStream(string fileName, int bufferSize = 4096)
        {
            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize))
                {
                }
            }

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize);
        }

        public bool CollectionExists(ulong collectionId)
        {
            return File.Exists(Path.Combine(Dir, collectionId + ".vec"));
        }

        public bool CollectionIsIndexOnly(ulong collectionId)
        {
            if (!CollectionExists(collectionId))
                throw new InvalidOperationException($"{collectionId} dows not exist");

            return !File.Exists(Path.Combine(Dir, collectionId + ".docs"));
        }

        public void Dispose()
        {
        }
    }
}