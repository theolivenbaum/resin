using System;
using System.Collections.Generic;

namespace DocumentTable
{
    public interface IReadSession : IDisposable
    {
        IList<Document> Read(IList<int> documentIds, BatchInfo ix);
    }
}
