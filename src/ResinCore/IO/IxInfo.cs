using System.Diagnostics;
using System.IO;
using log4net;

namespace Resin.IO
{
    [DebuggerDisplay("{VersionId}")]
    public class IxInfo
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (IxInfo));

        public long VersionId { get; set; }

        public int DocumentCount { get; set; }

        public Compression Compression { get; set; }

        public string PrimaryKeyFieldName { get; set; }

        public static IxInfo Load(string fileName)
        {
            var time = new Stopwatch();
            time.Start();

            IxInfo ix;

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ix = Serializer.DeserializeIxInfo(fs);
            }

            Log.DebugFormat("loaded ix in {0}", time.Elapsed);

            return ix;
        }
    }
}