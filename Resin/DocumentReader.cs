using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class DocumentReader
    {
        protected readonly string Directory;
        protected readonly Dictionary<string, DocFile> DocFiles;
        protected readonly Dictionary<string, Document> Docs; 
        private readonly Dictionary<string, List<string>> _docIdToFileIds;

        public DocumentReader(string directory, Dictionary<string, List<string>> docIdToFileIds)
        {
            Directory = directory;
            _docIdToFileIds = docIdToFileIds;
            DocFiles = new Dictionary<string, DocFile>();
            Docs = new Dictionary<string, Document>();
        }

        public IDictionary<string, string> GetDoc(string docId)
        {
            Document doc;
            if (Docs.TryGetValue(docId, out doc)) return doc.Fields;
            var upserted = new Document();
            var files = GetDocFiles(docId).ToList();
            foreach (var file in files)
            {
                var version = file.Docs[docId].Fields;
                foreach (var field in version)
                {
                    upserted.Fields[field.Key] = field.Value;
                }
            }
            Docs[docId] = upserted;
            return upserted.Fields;
        }

        protected IEnumerable<DocFile> GetDocFiles(string docId)
        {
            var fileIds = _docIdToFileIds[docId];
            foreach (var fileId in fileIds)
            {
                var fileName = Path.Combine(Directory, fileId + ".d");
                DocFile file;
                if (!DocFiles.TryGetValue(fileId, out file))
                {
                    file = DocFile.Load(fileName);
                    DocFiles[fileId] = file;
                }
                yield return file;  
            }
        }
    }
}