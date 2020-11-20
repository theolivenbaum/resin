using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public interface ISearchSession : IDisposable
    {
        SearchResult Search(Query query, int skip, int take, string primaryKey = null);
        SearchResult Search(Term term, int skip, int take, HashSet<string> select);
    }
}