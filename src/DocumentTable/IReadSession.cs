using System;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public interface IReadSession : IDisposable
    {
        BatchInfo Version { get; set; }
        IList<Document> ReadDocuments(IList<int> documentIds);
        DocHash ReadDocHash(int docId);
        Stream Stream { get; }
    }
}
