using System.Collections;
using System.Collections.Generic;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        void Write(string collectionId, IEnumerable<IDictionary> data);
    }
}
