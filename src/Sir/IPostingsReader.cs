using System;
using System.Collections.Generic;

namespace Sir
{
    public interface IPostingsReader : IDisposable
    {
        void Reduce(IEnumerable<Query> mappedQuery, IDictionary<long, double> result);
        IDictionary<long, double> ReadWithScore(IList<long> offsets, double score);
    }
}
