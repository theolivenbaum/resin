using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir
{
    public class Result
    {
        public IList<IDictionary> Documents { get; set; }
        public Stream Data { get; set; }
        public long Total { get; set; }
        public string MediaType { get; set; }
    }
}