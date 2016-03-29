using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class DocFile : FileBase<DocFile>
    {
        // docid/fields/value
        [ProtoMember(1)]
        private readonly IDictionary<int, IDictionary<string, string>> _docs;

        public DocFile()
        {
            _docs = new Dictionary<int, IDictionary<string, string>>();
        }

        public DocFile(IDictionary<int, IDictionary<string, string>> docs)
        {
            _docs = docs;
        }

        public IDictionary<int, IDictionary<string, string>> Docs
        {
            get { return _docs; }
        }
    }
}