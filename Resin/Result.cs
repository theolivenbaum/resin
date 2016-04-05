using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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

        public static ResolvedResult Parse(string source)
        {
            return JsonConvert.DeserializeObject<ResolvedResult>(source);
        }
    }
}