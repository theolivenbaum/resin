using System.Collections;
using System.Collections.Generic;

namespace Sir
{
    public interface IReader : IPlugin
    {
        IEnumerable<IDictionary> Read(Query query);
    }
}
