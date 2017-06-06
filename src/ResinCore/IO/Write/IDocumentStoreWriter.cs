using System;

namespace Resin.IO.Write
{
    public interface IDocumentStoreWriter : IDisposable
    {
        void Write(Document document);
    }
}
