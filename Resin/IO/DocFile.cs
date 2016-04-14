using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DocFile : FileBase<DocFile>
    {
        // docid/fields/value
        private readonly Dictionary<string, Document> _docs;

        public DocFile()
        {
            _docs = new Dictionary<string, Document>();
        }

        public DocFile(Dictionary<string, Document> docs)
        {
            _docs = docs;
        }

        public Dictionary<string, Document> Docs
        {
            get { return _docs; }
        }
    }
}