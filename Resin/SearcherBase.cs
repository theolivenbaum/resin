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
        protected DixFile Dix;
        protected FixFile Fix;

        protected SearcherBase(string directory, Dictionary<string, DocFile> docFiles, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles)
        {
            Directory = directory;
            DocFiles = docFiles;
            FieldFiles = fieldFiles;
            TrieFiles = trieFiles;
        }

        protected IDictionary<string, string> GetDoc(string docId)
        {
            var file = GetDocFile(docId);
            return file.Docs[docId].Fields;
        }

        protected DocFile GetDocFile(string docId)
        {
            var fileName = Path.Combine(Directory, Dix.DocIdToFileId[docId] + ".d");
            DocFile file;
            if (!DocFiles.TryGetValue(fileName, out file))
            {
                file = DocFile.Load(fileName);
                DocFiles[fileName] = file;
            }
            return file;
        }
    }
}