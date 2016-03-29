using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class FixFile : FileBase<FixFile>
    {
        // field/fileid
        [ProtoMember(1)]
        private readonly IDictionary<string, string> _fieldIndex;

        public FixFile()
        {
            _fieldIndex = new Dictionary<string, string>();
        }

        public IDictionary<string, string> FieldIndex
        {
            get { return _fieldIndex; }
        }
    }
}