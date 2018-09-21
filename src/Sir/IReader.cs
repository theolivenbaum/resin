using System.Collections;
using System.Collections.Generic;

namespace Sir
{
    public interface IReader : IPlugin
    {
        IList<IDictionary> Read(Query query, int take, out long total);
        IList<IDictionary> Read(Query query, out long total);
    }
}
