using System.Collections.Generic;
using ProtoBuf;

namespace Resin.IO
{
    [ProtoContract]
    public class DocFile : FileBase<DocFile>
    {
        // docid/fields/value
        [ProtoMember(1)]
        private readonly IDictionary<string, Document> _docs;

        public DocFile()
        {
            _docs = new Dictionary<string, Document>();
        }

        public DocFile(IDictionary<string, Document> docs)
        {
            _docs = docs;
        }

        public IDictionary<string, Document> Docs
        {
            get { return _docs; }
        }
    }
}