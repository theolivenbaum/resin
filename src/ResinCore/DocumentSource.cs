using Resin.IO;
using System.Collections.Generic;

namespace Resin
{
    public abstract class DocumentSource
    {
        public abstract IEnumerable<Document> ReadSource();
    }
}
