using Sir.VectorSpace;
using System;

namespace Sir.Search
{
    public interface ISearchSession : IDisposable
    {
        SearchResult Search(Query query, int skip, int take);
    }
}