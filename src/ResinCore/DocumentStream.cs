using System.Collections.Generic;

namespace Resin
{
    public abstract class DocumentStream
    {
        public abstract IEnumerable<Document> ReadSource();
    }
}