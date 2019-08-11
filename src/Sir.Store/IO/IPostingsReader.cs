using System;
using System.Collections.Generic;

namespace Sir.Store
{
    public interface IPostingsReader : IDisposable
    {
        void Reduce(IEnumerable<Query> mappedQuery, IDictionary<long, double> result);
    }
}
