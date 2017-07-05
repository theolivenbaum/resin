using System;

namespace DocumentTable
{
    public interface IDocumentStoreWriter : IDisposable
    {
        void Write(Document document);
    }
}
