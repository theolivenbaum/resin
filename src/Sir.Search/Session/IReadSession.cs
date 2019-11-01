using System;
using System.Collections.Generic;

namespace Sir.Store
{
    public interface IReadSession : IDisposable
    {
        void EnsureIsValid(Query query, long docId);
        ReadResult Read(IEnumerable<Query> query, int skip, int take);
        IList<IDictionary<string, object>> ReadDocs(IEnumerable<KeyValuePair<long, double>> docs);
    }
}