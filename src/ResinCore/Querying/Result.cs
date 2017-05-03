using System.Collections.Generic;

namespace Resin.Querying
{
    public class Result
    {
        public IList<ScoredDocument> Docs { get; set; }
        public int Total { get; set; }
    }
}