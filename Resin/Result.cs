using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class Result
    {
        public IEnumerable<IDictionary<string, string>> Docs { get; set; }
        public int Total { get; set; }
        public IDictionary<string, string> Trace { get; set; }

        public ResolvedResult Resolve()
        {
            var docs = Docs.ToList();
            var result = new ResolvedResult{Docs = docs.ToArray(), Total = Total, Trace = Trace};
            return result;
        }
    }

    public class ResolvedResult
    {
        public IDictionary<string,string>[] Docs { get; set; }
        public int Total { get; set; }
        public IDictionary<string, string> Trace { get; set; }
    }
}