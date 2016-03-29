using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class DocumentWriter
    {
        private bool _flushed;
        private readonly string _dir;

        // docid/fields/value
        private readonly IDictionary<int, IDictionary<string, string>> _docs;

        public DocumentWriter(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _dir = dir;
            _docs = new Dictionary<int, IDictionary<string, string>>();
        }

        public void Write(int docId, string field, string value)
        {
            IDictionary<string, string> doc;
            if (!_docs.TryGetValue(docId, out doc))
            {
                doc = new Dictionary<string, string>();
                _docs.Add(docId, doc);
            }
            doc[field] = value;
        }

        public void Flush(string dixFileName)
        {
            if (_flushed || _docs.Count == 0) return;

            // docid/file
            var dix = new DixFile();
            var batches = _docs.IntoBatches(1000).ToList();
            foreach (var batch in batches)
            {
                var docs = batch.ToDictionary(x => x.Key, y => y.Value);// TODO: fix crash when same doc appears twice in the same batch
                var d = new DocFile(docs);
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
            _flushed = true;
        }
    }
}