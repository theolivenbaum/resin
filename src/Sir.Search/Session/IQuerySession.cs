using System;

namespace Sir.Search
{
    public interface IQuerySession : IDisposable
    {
        ReadResult Query(IQuery query, int skip, int take, string primaryKey = null);
    }
}