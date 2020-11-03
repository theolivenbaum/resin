using Sir.VectorSpace;
using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public interface IQuerySession : IDisposable
    {
        ReadResult Query(IQuery query, int skip, int take, string primaryKey = null);
        ReadResult Query(Term term, int skip, int take, HashSet<string> select);
    }
}