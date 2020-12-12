using Microsoft.Extensions.Logging;
using Sir.Core;
using Sir.Documents;
using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Search
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ISessionFactory
    {
        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> _keys;

        public string Directory { get; }
        public ILogger Logger { get; }

        public SessionFactory(string directory = null, ILogger logger = null)
        {
            var time = Stopwatch.StartNew();

            Directory = directory;

            if (Directory != null && !System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.CreateDirectory(Directory);
            }

            _keys = LoadKeys();
            Logger = logger;

           Log($"sessionfactory initiated in {time.Elapsed}");
        }

        private void Log(string message)
        {
            if (Logger != null)
                Logger.LogInformation(message);
        }

        public long GetDocCount(string collection)
        {
            var fileName = Path.Combine(Directory, $"{collection.ToHash()}.dix");

            if (!File.Exists(fileName))
                return 0;

            return new FileInfo(fileName).Length / (sizeof(long) + sizeof(int));
        }

        public void Truncate(ulong collectionId)
        {
            var count = 0;

            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*"))
            {
                File.Delete(file);
                count++;
            }

            _keys.Remove(collectionId, out _);

            Log($"truncated collection {collectionId} ({count} files)");
        }

        public void TruncateIndex(ulong collectionId)
        {
            var count = 0;

            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*.ix"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*.ixp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*.ixtp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*.vec"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in System.IO.Directory.GetFiles(Directory, $"{collectionId}*.pos"))
            {
                File.Delete(file);
                count++;
            }

            Log($"truncated index {collectionId} ({count} files)");
        }

        public void Optimize(
            string collection,
            HashSet<string> selectFields, 
            HashSet<string> indexFields,
            ITextModel model,
            int skip = 0,
            int take = 0,
            int reportFrequency = 1000)
        {
            var collectionId = collection.ToHash();
            var debugger = new IndexDebugger(Logger, reportFrequency);

            TruncateIndex(collectionId);

            using (var docStream = new DocumentStreamSession(this))
            using (var documentReader = new DocumentReader(collectionId, this))
            using (var indexSession = new IndexSession<string>(model, model))
            {
                var payload = docStream.ReadDocs(
                        selectFields,
                        indexFields,
                        documentReader,
                        skip,
                        take);

                var batches = payload.Batch(reportFrequency);

                foreach (var batch in batches)
                {
                    Index(new TextJob(collectionId, batch, model), indexSession);

                    debugger.Step(indexSession, reportFrequency);
                }

                using (var stream = new WritableIndexStream(collectionId, this, logger: Logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }

            Log($"optimized collection {collection}");
        }

        public void SaveAs(
            ulong targetCollectionId, 
            IEnumerable<Document> documents,
            ITextModel model,
            int reportSize = 1000)
        {
            var job = new TextJob(targetCollectionId, documents, model);

            Write(job, reportSize);
        }

        public void Write(TextJob job, WriteSession writeSession, IndexSession<string> indexSession, int reportSize = 1000)
        {
            Log($"writing to collection {job.CollectionId}");

            var time = Stopwatch.StartNew();
            var debugger = new IndexDebugger(Logger, reportSize);

            foreach (var document in job.Documents)
            {
                writeSession.Put(document);

                //Parallel.ForEach(document, kv =>
                foreach (var field in document.Fields)
                {
                    if (field.Value != null && field.Index)
                    {
                        indexSession.Put(document.Id, field.KeyId, field.Value.ToString());
                    }
                }//);

                debugger.Step(indexSession);
            }

            Logger.LogInformation($"processed write&index job (collection {job.CollectionId}) in {time.Elapsed}");
        }

        public void Write(
            Document document, 
            WriteSession writeSession, 
            IndexSession<string> indexSession)
        {
            writeSession.Put(document);

            foreach (var field in document.Fields)
            {
                if (field.Value != null && field.Index)
                {
                    indexSession.Put(document.Id, field.KeyId, field.Value.ToString());
                }
            }
        }

        public void Index(TextJob job, int reportSize = 1000)
        {
            using (var indexSession = new IndexSession<string>(job.Model, job.Model))
            {
                Index(job, indexSession);

                using (var stream = new WritableIndexStream(job.CollectionId, this, logger: Logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }
        }

        public void Index<T>(TextJob job, IndexSession<T> indexSession)
        {
            Log($"indexing collection {job.CollectionId}");

            var time = Stopwatch.StartNew();

            using (var queue = new ProducerConsumerQueue<Document>(document =>
            {
                foreach (var field in document.Fields)
                {
                    if (field.Value != null && field.Index)
                    {
                        indexSession.Put(document.Id, field.KeyId, field.Tokens);
                    }
                }
            }))
            {
                foreach (var document in job.Documents)
                {
                    Parallel.ForEach(document.Fields, field =>
                    //foreach (var field in document.Fields)
                    {
                        if (field.Value != null && field.Index)
                        {
                            field.Analyze(job.Model);
                        }
                    });

                    queue.Enqueue(document);
                }
            }

            Log($"processed indexing job (collection {job.CollectionId}) in {time.Elapsed}");
        }

        public void Write(TextJob job, int reportSize = 1000)
        {
            using (var writeSession = new WriteSession(new DocumentWriter(job.CollectionId, this)))
            using (var indexSession = new IndexSession<string>(job.Model, job.Model))
            {
                Write(job, writeSession, indexSession, reportSize);

                using (var stream = new WritableIndexStream(job.CollectionId, this, logger: Logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }
        }

        public void Write(
            ulong collectionId,
            IEnumerable<Document> documents, 
            ITextModel model, 
            int reportSize = 1000
            )
        {
            using (var writeSession = new WriteSession(new DocumentWriter(collectionId, this)))
            using (var indexSession = new IndexSession<string>(model, model))
            {
                Write(
                    new TextJob(
                        collectionId,
                        documents,
                        model),
                    writeSession,
                    indexSession,
                    reportSize);

                using (var stream = new WritableIndexStream(collectionId, this, logger: Logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }
        }

        public FileStream CreateLockFile(ulong collectionId)
        {
            return new FileStream(Path.Combine(Directory, collectionId + ".lock"),
                   FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                   4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        }

        public void RefreshKeys()
        {
            _keys = LoadKeys();
        }

        public ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> LoadKeys()
        {
            var timer = Stopwatch.StartNew();
            var allkeys = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>>();

            if (Directory == null)
            {
                return allkeys;
            }

            foreach (var keyFile in System.IO.Directory.GetFiles(Directory, "*.kmap"))
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
            ConcurrentDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                keys = new ConcurrentDictionary<ulong, long>();
                _keys.GetOrAdd(collectionId, keys);
            }

            if (!keys.ContainsKey(keyHash))
            {
                keys.GetOrAdd(keyHash, keyId);

                using (var stream = CreateAppendStream(collectionId, "kmap"))
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

        public ISearchSession CreateQuerySession(IModel model)
        {
            return new SearchSession(
                this,
                model,
                new PostingsReader(this),
                Logger);
        }

        public Stream CreateAsyncReadStream(string fileName)
        {
            return File.Exists(fileName)
            ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous)
            : null;
        }

        public Stream CreateReadStream(string fileName)
        {
            return File.Exists(fileName)
                ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                : null;
        }

        public Stream CreateAsyncAppendStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        }

        public Stream CreateAppendStream(ulong collectionId, string fileExtension)
        {
            var fileName = Path.Combine(Directory, $"{collectionId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public Stream CreateAppendStream(ulong collectionId, long keyId, string fileExtension)
        {
            var fileName = Path.Combine(Directory, $"{collectionId}.{keyId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public bool CollectionExists(ulong collectionId)
        {
            return File.Exists(Path.Combine(Directory, collectionId + ".vec"));
        }

        public bool CollectionIsIndexOnly(ulong collectionId)
        {
            if (!CollectionExists(collectionId))
                throw new InvalidOperationException($"{collectionId} dows not exist");

            return !File.Exists(Path.Combine(Directory, collectionId + ".docs"));
        }

        public void Dispose()
        {
        }
    }
}