using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir
{
    /// <summary>
    /// Read/write result.
    /// </summary>
    public class ResultModel
    {
        public IList<IDictionary> Documents { get; set; }
        public MemoryStream Data { get; set; }
        public long Total { get; set; }
        public string MediaType { get; set; }
    }
}