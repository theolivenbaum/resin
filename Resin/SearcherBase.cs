using System.Collections.Generic;
using System.IO;
using Resin.IO;

namespace Resin
{
    public abstract class SearcherBase
    {
        protected readonly string Directory;
        protected readonly Dictionary<string, DocFile> DocFiles;
        protected readonly Dictionary<string, FieldFile> FieldFiles;
        protected readonly Dictionary<string, Trie> TrieFiles;
        protected readonly Dictionary<string, IDictionary<string, string>> Docs; 
        protected DixFile Dix;
        protected FixFile Fix;

        protected SearcherBase(string directory, Dictionary<string, DocFile> docFiles, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles, Dictionary<string, IDictionary<string, string>> docs)
        {
            Directory = directory;
            DocFiles = docFiles;
            FieldFiles = fieldFiles;
            TrieFiles = trieFiles;
            Docs = docs;
        }

        protected IDictionary<string, string> GetDoc(string docId)
        {
            IDictionary<string, string> doc;
            if (!Docs.TryGetValue(docId, out doc))
            {
                var file = GetDocFile(docId);
                doc = file.Docs[docId].Fields;                
            }
            return doc;
        }

        protected DocFile GetDocFile(string docId)
        {
            var fileId = Dix.DocIdToFileId[docId];
            var fileName = Path.Combine(Directory, fileId + ".d");
            DocFile file;
            if (!DocFiles.TryGetValue(fileId, out file))
            {
                file = DocFile.Load(fileName);
                DocFiles[fileId] = file;
            }
            return file;
        }
    }
}