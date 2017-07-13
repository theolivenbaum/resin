using System.Collections.Generic;

namespace Resin
{
    public class ScoredResult
    {
        public IList<ScoredDocument> Docs { get; set; }
        public int Total { get; set; }
    }
}