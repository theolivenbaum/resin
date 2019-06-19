using System.Collections.Generic;
using System.IO;

namespace Sir
{
    /// <summary>
    /// Read/write result.
    /// </summary>
    public class ResponseModel
    {
        public long? Id { get; set; }
        public IList<IDictionary<string, object>> Documents { get; set; }
        public MemoryStream Stream { get; set; }
        public long Total { get; set; }
        public string MediaType { get; set; }
    }
}