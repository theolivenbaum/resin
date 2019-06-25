using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// Read/write result.
    /// </summary>
    public class ResponseModel
    {
        public IList<IDictionary<string, object>> Documents { get; set; }
        public long Total { get; set; }
        public string MediaType { get; set; }
        public byte[] Body { get; set; }
    }
}