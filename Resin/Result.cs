using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class Result
    {
        public IEnumerable<Document> Docs { get; set; }
        public int Total { get; set; }

        public EagerResult Resolve()
        {
            var docs = Docs.ToList();
            var result = new EagerResult{Docs = docs.Select(d=>d.Fields).ToList(), Total = Total};
            return result;
        }
    }

    public class EagerResult
    {
        public IList<IDictionary<string,string>> Docs { get; set; }
        public int Total { get; set; }
    }

    public class DynamicResult
    {
        public IList<dynamic> Docs { get; set; }
        public int Total { get; set; }
    }
}