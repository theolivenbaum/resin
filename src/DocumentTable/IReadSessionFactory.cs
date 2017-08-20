using System;

namespace DocumentTable
{
    public interface IReadSessionFactory : IDisposable
    {
        string DirectoryName { get; }
        IReadSession OpenReadSession(long version);
        IReadSession OpenReadSession(SegmentInfo version);
    }
}
