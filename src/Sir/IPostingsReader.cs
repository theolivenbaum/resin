using System;
using System.Collections.Generic;

namespace Sir
{
    public interface IPostingsReader : IDisposable
    {
        void Reduce(IQuery query, ref IDictionary<(ulong, long), double> result);
        void Reduce(Term term, ref IDictionary<(ulong, long), double> result);
    }
}