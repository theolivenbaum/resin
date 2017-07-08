using System;
using System.Collections.Generic;

namespace DocumentTable
{
    public interface IReadSession : IDisposable
    {
        BatchInfo Version { get; set; }
        IList<Document> ReadDocuments(IList<int> documentIds);
        IList<IList<DocumentPosting>> ReadPostings(IList<Term> terms);
        DocHash ReadDocHash(int docId);
    }
}
