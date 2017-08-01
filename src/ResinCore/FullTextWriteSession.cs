using DocumentTable;
using System.IO;

namespace Resin
{
    public class FullTextWriteSession : WriteSession
    {
        public FullTextWriteSession(string directory, FullTextSegmentInfo ix, Stream dataFile)
            :base(directory, ix, dataFile)
        {
        }
        
        protected override void SaveSegmentInfo(SegmentInfo ix)
        {
            IO.Serializer.Serialize(
                (FullTextSegmentInfo)ix, Path.Combine(_directory, _ix.VersionId + ".ix"));
        }
    }


}
