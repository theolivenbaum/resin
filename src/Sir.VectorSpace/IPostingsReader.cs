using System;
using System.Collections.Generic;

namespace Sir.VectorSpace
{
    public interface IPostingsReader : IDisposable
    {
        void Map(Query query);
        void Reduce(Query query, ref IDictionary<(ulong, long), double> result);
        void Reduce(Term term, ref IDictionary<(ulong, long), double> result);
    }
}