using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class DocContainer : CompressedFileBase<DocContainer>
    {
        private readonly string _id;
        public string Id { get { return _id; } }

        /// <summary>
        /// docid/file
        /// </summary>
        private readonly Dictionary<string, Document> _files;

        public DocContainer(string id)
        {
            _id = id;
            _files = new Dictionary<string, Document>();
        }

        public Document Get(string docId)
        {
            return _files[docId];
        }

        public void Put(Document doc)
        {
            _files[doc.Id] = doc;
        }

        public bool TryGet(string docId, out Document doc)
        {
            return _files.TryGetValue(docId, out doc);
        }

        public void Remove(string docId)
        {
            _files.Remove(docId);
        }

        public int Count { get { return _files.Count; } }
    }
}