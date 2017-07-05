using System;
using System.Collections.Generic;

namespace DocumentTable
{
    public interface IReadSession : IDisposable
    {
        IEnumerable<Document> Read(IList<int> documentIds);
    }
}
