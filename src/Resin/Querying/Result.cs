using System.Collections.Generic;

namespace Resin.Querying
{
    public class Result
    {
        public IEnumerable<Document> Docs { get; set; }
        public int Total { get; set; }
    }
}