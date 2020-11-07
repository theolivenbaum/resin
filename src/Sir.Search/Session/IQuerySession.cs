using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public interface IQuerySession : IDisposable
    {
        SearchResult Search(IQuery query, int skip, int take, string primaryKey = null);
        SearchResult Search(Term term, int skip, int take, HashSet<string> select);
    }
}