using Microsoft.Extensions.Logging;
using Sir.Core;
using Sir.Documents;
using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Search
{
    /// <summary>
    /// Multi-directory stream dispatcher with helper methods for writing, indexing, optimizing and truncating collections.
    /// </summary>
    public class Database : IDisposable, IDatabase
    {
        private IDictionary<ulong, IDictionary<ulong, long>> _keys;
        private ILogger _logger;
        private readonly object _syncKeys = new object();

        public Database(ILogger logger = null)
        {
            _logger = logger;
            _keys = new Dictionary<ulong, IDictionary<ulong, long>>();

            LogInformation($"database initiated");
        }

        public void LogInformation(string message)
        {
            if (_logger != null)
                _logger.LogInformation(message);
        }

        public void LogDebug(string message)
        {
            if (_logger != null)
                _logger.LogDebug(message);
        }

        public long GetDocCount(string directory, string collection)
        {
            var fileName = Path.Combine(directory, $"{collection.ToHash()}.dix");

            if (!File.Exists(fileName))
                return 0;

            return new FileInfo(fileName).Length / (sizeof(long) + sizeof(int));
        }

        public void Truncate(string directory, ulong collectionId)
        {
            var count = 0;

            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*"))
            {
                File.Delete(file);
                count++;
            }

            lock (_syncKeys)
            {
                _keys.Remove(collectionId, out _);
            }

            LogInformation($"truncated collection {collectionId} ({count} files affected)");
        }

        public void TruncateIndex(string directory, ulong collectionId)
        {
            var count = 0;

            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*.ix"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*.ixp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*.ixtp"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*.vec"))
            {
                File.Delete(file);
                count++;
            }
            foreach (var file in Directory.GetFiles(directory, $"{collectionId}*.pos"))
            {
                File.Delete(file);
                count++;
            }

            LogInformation($"truncated index {collectionId} ({count} files affected)");
        }

        public void Rename(string directory, ulong currentCollectionId, ulong newCollectionId)
        {
            var count = 0;

            var from = currentCollectionId.ToString();
            var to = newCollectionId.ToString();

            foreach (var file in Directory.GetFiles(directory, $"{currentCollectionId}*"))
            {
                File.Move(file, file.Replace(from, to));
                count++;
            }

            _keys.Remove(currentCollectionId, out _);

            LogInformation($"renamed collection {currentCollectionId} to {newCollectionId} ({count} files affected)");
        }

        public void Optimize<T>(
            string directory,
            string collection,
            HashSet<string> selectFields, 
            IModel<T> model,
            int skipDocuments = 0,
            int takeDocuments = 0,
            int reportFrequency = 1000,
            int pageSize = 100000,
            bool truncateIndex = true)
        {
            var collectionId = collection.ToHash();

            if (truncateIndex)
                TruncateIndex(directory, collectionId);

            using (var debugger = new IndexDebugger(_logger, reportFrequency))
            using (var documents = new DocumentStreamSession(directory, this))
            {
                using (var writeQueue = new ProducerConsumerQueue<IndexSession<T>>(indexSession =>
                {
                    using (var stream = new WritableIndexStream(directory, collectionId, this, logger: _logger))
                    {
                        stream.Write(indexSession.GetInMemoryIndex());
                    }
                }))
                {
                    var took = 0;
                    var skip = skipDocuments;

                    while (took < takeDocuments)
                    {
                        var payload = documents.ReadDocumentVectors(
                            collectionId,
                            selectFields,
                            model,
                            skip,
                            pageSize);

                        var count = 0;

                        using (var indexSession = new IndexSession<T>(model, model))
                        {
                            Parallel.ForEach(payload, document =>
                            {
                                foreach (var node in document.Nodes)
                                {
                                    indexSession.Put(node);
                                }

                                Interlocked.Increment(ref count);

                                debugger.Step(indexSession);
                            });

                            writeQueue.Enqueue(indexSession);
                        }

                        if (count == 0)
                            break;

                        took += count;
                        skip += pageSize;
                    }
                }
            }

            LogInformation($"optimized collection {collection}");
        }

        public void SaveAs<T>(
            string targetDirectory,
            ulong targetCollectionId, 
            IEnumerable<Document> documents,
            IModel<T> model,
            int reportSize = 1000)
        {
            Write(targetDirectory, targetCollectionId, documents, model, reportSize);
        }

        public void Put<T>(IEnumerable<Document> job, WriteSession writeSession, IndexSession<T> indexSession, int reportSize = 1000)
        {
            var debugger = new IndexDebugger(_logger, reportSize);

            foreach (var document in job)
            {
                writeSession.Put(document);

                foreach (var field in document.Fields)
                {
                    if (field.Value != null && field.Index)
                    {
                        indexSession.Put(document.Id, field.KeyId, (T)field.Value);
                    }
                }

                debugger.Step(indexSession);
            }
        }

        public void Put<T>(
            Document document, 
            WriteSession writeSession, 
            IndexSession<T> indexSession)
        {
            writeSession.Put(document);

            foreach (var field in document.Fields)
            {
                if (field.Value != null && field.Index)
                {
                    indexSession.Put(document.Id, field.KeyId, (T)field.Value);
                }
            }
        }

        public void Index<T>(string directory, ulong collectionId, IEnumerable<Document> job, IModel<T> model, int reportSize = 1000)
        {
            using (var indexSession = new IndexSession<T>(model, model))
            {
                Index(collectionId, job, model, indexSession);

                using (var stream = new WritableIndexStream(directory, collectionId, this, logger: _logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }
        }

        public void Index<T>(ulong collectionId, IEnumerable<Document> job, IModel<T> model, IndexSession<T> indexSession)
        {
            LogInformation($"indexing collection {collectionId}");

            var time = Stopwatch.StartNew();

            using (var queue = new ProducerConsumerQueue<Document>(document =>
            {
                foreach (var field in document.Fields)
                {
                    if (field.Value != null && field.Index)
                    {
                        indexSession.Put(field.DocumentId, field.KeyId, field.Tokens);
                    }
                }
            }))
            {
                foreach (var document in job)
                {
                    foreach (var field in document.Fields)
                    {
                        if (field.Value != null && field.Index)
                        {
                            field.Analyze(model);
                        }
                    }

                    queue.Enqueue(document);
                }
            }

            LogInformation($"processed indexing job (collection {collectionId}) in {time.Elapsed}");
        }

        public void Write<T>(string directory, ulong collectionId, IEnumerable<Document> job, IModel<T> model, int reportSize = 1000)
        {
            using (var writeSession = new WriteSession(new DocumentWriter(directory, collectionId, this)))
            using (var indexSession = new IndexSession<T>(model, model))
            {
                Put(job, writeSession, indexSession, reportSize);

                using (var stream = new WritableIndexStream(directory, collectionId, this, logger: _logger))
                {
                    stream.Write(indexSession.GetInMemoryIndex());
                }
            }
        }

        public void Store(string directory, ulong collectionId, IEnumerable<Document> job)
        {
            using (var writeSession = new WriteSession(new DocumentWriter(directory, collectionId, this)))
            {
                foreach (var document in job)
                    writeSession.Put(document);
            }
        }

        public void Update(string directory, ulong collectionId, long documentId, long keyId, object value)
        {
            using (var updateSession = new UpdateSession(directory, collectionId, this))
            {
                updateSession.Update(documentId, keyId, value);
            }
        }

        public FileStream CreateLockFile(string directory, ulong collectionId)
        {
            return new FileStream(Path.Combine(directory, collectionId + ".lock"),
                   FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                   4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        }

        private void ReadKeys(string directory)
        {
            foreach (var keyFile in Directory.GetFiles(directory, "*.kmap"))
            {
                var collectionId = ulong.Parse(Path.GetFileNameWithoutExtension(keyFile));
                IDictionary<ulong, long> keys;

                if (!_keys.TryGetValue(collectionId, out keys))
                {
                    keys = new Dictionary<ulong, long>();

                    var timer = Stopwatch.StartNew();

                    using (var stream = new FileStream(keyFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long i = 0;
                        var buf = new byte[sizeof(ulong)];
                        var read = stream.Read(buf, 0, buf.Length);

                        while (read > 0)
                        {
                            keys.Add(BitConverter.ToUInt64(buf, 0), i++);

                            read = stream.Read(buf, 0, buf.Length);
                        }
                    }

                    lock (_syncKeys)
                    {
                        _keys.Add(collectionId, keys);
                    }

                    LogInformation($"loaded key mappings into memory from directory {directory} in {timer.Elapsed}");
                }
            }
        }

        public void RegisterKeyMapping(string directory, ulong collectionId, ulong keyHash, long keyId)
        {
            if (!_keys.TryGetValue(collectionId, out _))
            {
                ReadKeys(directory);
            }

            IDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                keys = new ConcurrentDictionary<ulong, long>();

                lock (_syncKeys)
                {
                    _keys.Add(collectionId, keys);
                }
            }

            if (!keys.ContainsKey(keyHash))
            {
                keys.Add(keyHash, keyId);

                using (var stream = CreateAppendStream(directory, collectionId, "kmap"))
                {
                    stream.Write(BitConverter.GetBytes(keyHash), 0, sizeof(ulong));
                }
            }
        }

        public long GetKeyId(string directory, ulong collectionId, ulong keyHash)
        {
            IDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                ReadKeys(directory);
            }

            if (keys != null || _keys.TryGetValue(collectionId, out keys))
            {
                return keys[keyHash];
            }

            throw new Exception($"unable to find key {keyHash} for collection {collectionId} in directory {directory}.");
        }

        public bool TryGetKeyId(string directory, ulong collectionId, ulong keyHash, out long keyId)
        {
            IDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                ReadKeys(directory);
            }

            if (keys != null || _keys.TryGetValue(collectionId, out keys))
            {
                if (keys.TryGetValue(keyHash, out keyId))
                {
                    return true;
                }
            }

            keyId = -1;
            return false;
        }

        public Stream CreateAsyncReadStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        }

        public Stream CreateReadStream(string fileName)
        {
            LogDebug($"opened {fileName}");

            return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public Stream CreateAsyncAppendStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        }

        public Stream CreateAppendStream(string directory, ulong collectionId, string fileExtension)
        {
            var fileName = Path.Combine(directory, $"{collectionId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public Stream CreateAppendStream(string directory, ulong collectionId, long keyId, string fileExtension)
        {
            var fileName = Path.Combine(directory, $"{collectionId}.{keyId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public Stream CreateSeekableWritableStream(string directory, ulong collectionId, long keyId, string fileExtension)
        {
            var fileName = Path.Combine(directory, $"{collectionId}.{keyId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }

        public Stream CreateSeekableWritableStream(string directory, ulong collectionId, string fileExtension)
        {
            var fileName = Path.Combine(directory, $"{collectionId}.{fileExtension}");

            if (!File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
            }

            return new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }

        public bool CollectionExists(string directory, ulong collectionId)
        {
            return File.Exists(Path.Combine(directory, collectionId + ".vec"));
        }

        public bool CollectionIsIndexOnly(string directory, ulong collectionId)
        {
            if (!CollectionExists(directory, collectionId))
                throw new InvalidOperationException($"{collectionId} dows not exist");

            return !File.Exists(Path.Combine(directory, collectionId + ".docs"));
        }

        public void Dispose()
        {
            LogInformation($"sessionfactory disposed");
        }
    }
}