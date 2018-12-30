using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class ReadResult
    {
        public long Total { get; set; }
        public IList<IDictionary> Docs { get; set; }
    }
}
