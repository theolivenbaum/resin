using System;

namespace Resin.DocumentSearchServer
{
    public class ResultInfo
    {
        public int Total { get; set; }
        public int Count { get; set; }
        public TimeSpan Elapsed { get; set; }
    }
}
