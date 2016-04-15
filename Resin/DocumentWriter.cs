using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class DocumentWriter
    {
        private bool _flushing;
        private readonly string _dir;

        // docid/fields/value
        private readonly IDictionary<string, Document> _docs;

        public DocumentWriter(string dir)
        {
            _dir = dir;
            _docs = new Dictionary<string, Document>();
        }

        public void Write(Document doc)
        {
            _docs[doc.Id] = doc; // this overwrites previous doc if same docId appears twice in the session
        }

        public void Flush(string dixFileName)
        {
            if (_flushing) return;

            _flushing = true;

            // docid/file
            var dix = new DixFile();
            var batches = _docs.IntoBatches(1000).ToList();
            foreach (var batch in batches)
            {
                var d = new DocFile(batch.ToDictionary(x=>x.Key, y=>y.Value));
                var fileId = Path.GetRandomFileName();
                var fileName = Path.Combine(_dir, fileId + ".d");
                d.Save(fileName);
                foreach (var docId in d.Docs)
                {
                    dix.DocIdToFileIndex[docId.Key] = fileId;
                }
            }
            dix.Save(dixFileName);
            _docs.Clear();
        }
    }
}