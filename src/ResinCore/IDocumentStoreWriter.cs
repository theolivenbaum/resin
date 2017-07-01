using System;

namespace Resin
{
    public interface IDocumentStoreWriter : IDisposable
    {
        void Write(Document document);
    }
}
