using System;

namespace DocumentTable
{
    public interface IWriteSession : IDisposable
    {
        SegmentInfo Version { get; }
        void Write(Document document);
        void Flush();
        SegmentInfo Commit();
    }
}
