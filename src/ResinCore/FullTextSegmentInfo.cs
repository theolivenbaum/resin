using DocumentTable;
using Resin.IO;
using System.Diagnostics;
using System.IO;

namespace Resin
{
    public class FullTextSegmentInfo : SegmentInfo
    {
        public new static FullTextSegmentInfo Load(string directory, long version)
        {
            return Load(Path.Combine(directory, version + ".ix"));
        }

        public new static FullTextSegmentInfo Load(string fileName)
        {
            var time = new Stopwatch();
            time.Start();

            FullTextSegmentInfo ix;

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ix = Serializer.DeserializeSegmentInfo(fs);
            }

            Log.DebugFormat("loaded ix in {0}", time.Elapsed);

            return ix;
        }
    }
}
