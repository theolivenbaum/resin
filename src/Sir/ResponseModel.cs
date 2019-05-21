using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir
{
    /// <summary>
    /// Read/write result.
    /// </summary>
    public class ResponseModel
    {
        public IList<IDictionary<string, object>> Documents { get; set; }
        public MemoryStream Stream { get; set; }
        public long Total { get; set; }
        public string MediaType { get; set; }
    }
}