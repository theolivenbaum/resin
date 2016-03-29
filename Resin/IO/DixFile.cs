using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class DixFile : FileBase<DixFile>
    {
        // docid/file
        [ProtoMember(1)]
        private readonly IDictionary<string, string>_docIdToFileIndex;

        public DixFile()
        {
            _docIdToFileIndex = new Dictionary<string, string>();
        }

        public IDictionary<string, string> DocIdToFileIndex
        {
            get { return _docIdToFileIndex; }
        }
    }
}