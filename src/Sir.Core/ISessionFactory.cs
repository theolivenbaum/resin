using System.IO;

namespace Sir
{
    public interface ISessionFactory
    {
        //string Directory { get; }
        Stream CreateAppendStream(string directory, ulong collectionId, string fileExtension);
        Stream CreateAppendStream(string directory, ulong collectionId, long keyId, string fileExtension);
        Stream CreateAsyncAppendStream(string fileName);
        Stream CreateAsyncReadStream(string fileName);
        Stream CreateReadStream(string fileName);
        void RegisterKeyMapping(string directory, ulong collectionId, ulong keyHash, long keyId);
        bool TryGetKeyId(string directory, ulong collectionId, ulong keyHash, out long keyId);
        void LogDebug(string message);
        void LogInformation(string message);
    }
}