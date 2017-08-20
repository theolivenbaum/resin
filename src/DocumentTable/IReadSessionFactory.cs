using System;

namespace Resin.Documents
{
    public interface IReadSessionFactory : IDisposable
    {
        string DirectoryName { get; }
        IReadSession OpenReadSession(long version);
        IReadSession OpenReadSession(SegmentInfo version);
    }
}
