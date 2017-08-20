using System;

namespace Resin.SearchServer
{
    public class ResultInfo
    {
        public int Total { get; set; }
        public int Count { get; set; }
        public TimeSpan Elapsed { get; set; }
    }
}
