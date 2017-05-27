using System.Collections.Generic;

namespace Resin
{
    public class Result
    {
        public IList<ScoredDocument> Docs { get; set; }
        public int Total { get; set; }
        public string[] QueryTerms { get; set; }
    }
}