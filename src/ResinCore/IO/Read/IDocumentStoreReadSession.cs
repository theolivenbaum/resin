using System;
using System.Collections.Generic;

namespace Resin.IO.Read
{
    public interface IDocumentStoreReadSession : IDisposable
    {
        IEnumerable<Document> Read(IList<int> documentIds);
    }
}
