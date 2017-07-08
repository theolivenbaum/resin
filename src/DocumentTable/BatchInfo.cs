using System.Diagnostics;
using System.IO;
using log4net;
using System.Collections.Generic;

namespace DocumentTable
{
    [DebuggerDisplay("{VersionId}")]
    public class BatchInfo
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (BatchInfo));

        public long VersionId { get; set; }

        public int DocumentCount { get; set; }

        public Compression Compression { get; set; }

        public string PrimaryKeyFieldName { get; set; }

        public long PostingsOffset { get; set; }

        public long DocHashOffset { get; set; }

        public long DocAddressesOffset { get; set; }

        public IDictionary<ulong, long> FieldOffsets { get; set; }

        public static BatchInfo Load(string fileName)
        {
            var time = new Stopwatch();
            time.Start();

            BatchInfo ix;

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ix = TableSerializer.DeserializeBatchInfo(fs);
            }

            Log.DebugFormat("loaded ix in {0}", time.Elapsed);

            return ix;
        }
    }
}