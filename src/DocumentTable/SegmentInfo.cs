using System.Diagnostics;
using System.IO;
using log4net;
using System.Collections.Generic;

namespace DocumentTable
{
    [DebuggerDisplay("{Version}")]
    public class SegmentInfo
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof (SegmentInfo));

        public long Version { get; set; }

        public int DocumentCount { get; set; }

        public Compression Compression { get; set; }

        public string PrimaryKeyFieldName { get; set; }

        public long PostingsOffset { get; set; }

        public long DocHashOffset { get; set; }

        public long DocAddressesOffset { get; set; }

        public IDictionary<ulong, long> FieldOffsets { get; set; }

        public long KeyIndexOffset { get; set; }

        public int KeyIndexSize { get; set; }

        public long Length { get; set; }

        public static SegmentInfo Load(string directory, long version)
        {
            return Load(Path.Combine(directory, version + ".ix"));
        }

        public static SegmentInfo Load(string fileName)
        {
            var time = new Stopwatch();
            time.Start();

            SegmentInfo ix;

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ix = DocumentSerializer.DeserializeSegmentInfo(fs);
            }

            Log.DebugFormat("loaded ix {0} in {1}", fileName, time.Elapsed);

            return ix;
        }
    }
}