using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Sir
{
    public interface ISessionFactory
    {
        IConfigurationProvider Config { get; }
        string Dir { get; }
        IStringModel Model { get; }

        void ClearPageInfo();
        bool CollectionExists(ulong collectionId);
        Stream CreateAppendStream(string fileName, int bufferSize = 4096);
        Stream CreateAsyncAppendStream(string fileName, int bufferSize = 4096);
        Stream CreateAsyncReadStream(string fileName, int bufferSize = 4096);
        Stream CreateReadStream(string fileName, int bufferSize = 4096, FileOptions fileOptions = FileOptions.RandomAccess);
        void Dispose();
        IList<(long offset, long length)> GetAllPages(string pageFileName);
        long GetDocCount(string collection);
        long GetKeyId(ulong collectionId, ulong keyHash);
        ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> LoadKeys();
        MemoryMappedFile OpenMMF(string fileName);
        void PersistKeyMapping(ulong collectionId, ulong keyHash, long keyId);
        void RefreshKeys();
        void Truncate(ulong collectionId);
        void TruncateIndex(ulong collectionId);
        bool TryGetKeyId(ulong collectionId, ulong keyHash, out long keyId);
        void WriteConcurrent(Job job);
        void WriteConcurrent(IEnumerable<IDictionary<string, object>> documents, IStringModel model);
    }
}