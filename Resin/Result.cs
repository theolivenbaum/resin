using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;

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

    public class DynamicResult
    {
        public dynamic[] Docs { get; set; }
        public int Total { get; set; }
        public dynamic Trace { get; set; }
    }

    public static class ResultHelper
    {
        public static IDictionary<string, string> ToTraceDictionary(this DynamicResult result)
        {
            var d = new RouteValueDictionary(result.Trace);
            return d.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        } 
    }
}