using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class DixFile : FileBase<DixFile>
    {
        // docid/file
        [ProtoMember(1)]
        private readonly IDictionary<int, string>_docIdToFileIndex;

        public DixFile()
        {
            _docIdToFileIndex = new Dictionary<int, string>();
        }

        public IDictionary<int, string> DocIdToFileIndex
        {
            get { return _docIdToFileIndex; }
        }
    }
}