using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class DocFile : FileBase<DocFile>
    {
        // docid/fields/value
        [ProtoMember(1)]
        private readonly IDictionary<string, IDictionary<string, string>> _docs;

        public DocFile()
        {
            _docs = new Dictionary<string, IDictionary<string, string>>();
        }

        public DocFile(IDictionary<string, IDictionary<string, string>> docs)
        {
            _docs = docs;
        }

        public IDictionary<string, IDictionary<string, string>> Docs
        {
            get { return _docs; }
        }
    }
}