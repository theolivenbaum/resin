using System.Collections.Generic;
using System.Linq;
using Resin.IO;

namespace Resin.Querying
{
    public class Result
    {
        public IEnumerable<Document> Docs { get; set; }
        public int Total { get; set; }

        public ResolvedResult Resolve()
        {
            var docs = Docs.ToList();
            var result = new ResolvedResult{Docs = docs.ToArray(), Total = Total};
            return result;
        }
    }

    public class ResolvedResult
    {
        public IList<Document> Docs { get; set; }
        public int Total { get; set; }
    }
}