using System;

namespace Resin.Documents
{
    public interface IWriteSession : IDisposable
    {
        SegmentInfo Version { get; }
        void Write(DocumentTableRow document);
        void Flush();
        SegmentInfo Commit();
    }
}
