using System;

namespace DocumentTable
{
    public interface IWriteSession : IDisposable
    {
        void Write(Document document);
        void Flush();
    }
}
