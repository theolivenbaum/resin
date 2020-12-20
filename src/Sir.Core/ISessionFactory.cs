using System.IO;

namespace Sir
{
    public interface ISessionFactory
    {
        string Directory { get; }
        Stream CreateAppendStream(ulong collectionId, string fileExtension);
        Stream CreateAppendStream(ulong collectionId, long keyId, string fileExtension);
        Stream CreateAsyncAppendStream(string fileName);
        Stream CreateAsyncReadStream(string fileName);
        Stream CreateReadStream(string fileName);
        void RegisterKeyMapping(ulong collectionId, ulong keyHash, long keyId);
        bool TryGetKeyId(ulong collectionId, ulong keyHash, out long keyId);
        void LogDebug(string message);
        void LogInformation(string message);
    }
}