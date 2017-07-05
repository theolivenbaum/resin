using System;
using System.Collections.Generic;

namespace DocumentTable
{
    public interface IDocumentStoreReadSession : IDisposable
    {
        IEnumerable<Document> Read(IList<int> documentIds);
    }
}
