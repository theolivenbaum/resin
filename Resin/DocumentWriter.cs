using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Resin.IO;

namespace Resin
{
    public class DocumentWriter
    {
        private bool _flushing;
        private readonly string _dir;
        private readonly int _batchSize;

        // docid/fields/value
        private readonly IDictionary<string, Document> _docs;

        public DocumentWriter(string dir, IDictionary<string, Document> docs, int batchSize = 1000)
        {
            _dir = dir;
            _batchSize = batchSize;
            _docs = docs;
        }

        public void Flush(DixFile dix)
        {
            if (_flushing) return;

            _flushing = true;

            var files = new Dictionary<string, DocFile>();
            var batches = _docs.IntoBatches(_batchSize).ToList();
            foreach (var batch in batches)
            {
                var fileId = Path.GetRandomFileName();
                var d = new DocFile(batch.ToDictionary(x => x.Key, y => y.Value));
                foreach (var docId in d.Docs)
                {
                    dix.DocIdToFileId[docId.Key] = fileId;
                }
                files.Add(fileId, d);
            }

            //foreach (var d in files)
            Parallel.ForEach(files, d =>
            {
                var fileName = Path.Combine(_dir, d.Key + ".d");
                d.Value.Save(fileName);

            });
            _docs.Clear();
        }
    }
}