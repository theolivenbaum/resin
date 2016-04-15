using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Optimizer : SearcherBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Optimizer));
        private readonly IList<string> _generations;

        public Optimizer(string directory, IList<string> generations, Dictionary<string, DocFile> docFiles, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles, DixFile dix, FixFile fix)
            : base(directory, docFiles, fieldFiles, trieFiles)
        {
            _generations = generations;
            Dix = dix;
            Fix = fix;
        }

        public void Optimize()
        {
            Rebase(_generations); 
        }

        public void Flush()
        {
            
        }

        /// <summary>
        /// Newer generation indices are treated as changesets to older generations.
        /// A changeset (i.e. an index) can contain both (1) document deletions as well as (2) upserts of document fields.
        /// </summary>
        /// <param name="generations">The *.ix file names of the subsequent generations sorted by age, oldest first.</param>
        private void Rebase(IEnumerable<string> generations)
        {
            var rebasedDocs = new Dictionary<string, Document>();
            foreach (var gen in generations)
            {
                var ix = IxFile.Load(gen);
                foreach (var term in ix.Deletions)
                {
                    var collector = new Collector(Directory, Fix, FieldFiles, TrieFiles);
                    var docIds = collector.Collect(new QueryContext(term.Field, term.Token), 0, int.MaxValue).Select(ds => ds.DocId).ToList();
                    foreach (var docId in docIds)
                    {
                        var docFile = GetDocFile(docId);
                        docFile.Docs.Remove(docId);
                        DocFiles[Path.Combine(Directory, Dix.DocIdToFileIndex[docId] + ".d")] = docFile;
                        Dix.DocIdToFileIndex.Remove(docId);

                        foreach (var field in Fix.FieldIndex)
                        {
                            var fileName = Path.Combine(Directory, field.Value + ".f");
                            FieldFile ff;
                            if (!FieldFiles.TryGetValue(fileName, out ff))
                            {
                                ff = FieldFile.Load(fileName);
                            }
                            ff.Remove(docId);
                            FieldFiles[fileName] = ff;
                        }
                    }
                }

                var dix = DixFile.Load(Path.Combine(Directory, ix.DixFileName));
                foreach (var newDoc in dix.DocIdToFileIndex)
                {
                    var docFile = GetDocFile(newDoc.Key, dix);
                    var nd = docFile.Docs[newDoc.Key];
                    if (Dix.DocIdToFileIndex.ContainsKey(newDoc.Key))
                    {
                        var oldDoc = GetDoc(newDoc.Key);
                        foreach (var field in nd.Fields)
                        {
                            oldDoc[field.Key] = field.Value; // upsert of field
                        }
                        nd = new Document(oldDoc);
                    }
                    else
                    {
                        Dix.DocIdToFileIndex[newDoc.Key] = newDoc.Value;
                    }
                    rebasedDocs[nd.Id] = nd;
                }

                var fix = FixFile.Load(Path.Combine(Directory, ix.FixFileName));
                foreach (var field in fix.FieldIndex)
                {
                    var newFileName = Path.Combine(Directory, field.Value + ".f");
                    var oldFileName = Path.Combine(Directory, Fix.FieldIndex[field.Key] + ".f");
                    FieldFile oldFile;
                    if (!FieldFiles.TryGetValue(oldFileName, out oldFile))
                    {
                        oldFile = FieldFile.Load(oldFileName);
                    }
                    var newFile = FieldFile.Load(newFileName);
                    foreach (var entry in newFile.Tokens)
                    {
                        Dictionary<string, int> oldFilePostings;
                        if (!oldFile.Tokens.TryGetValue(entry.Key, out oldFilePostings))
                        {
                            oldFile.Tokens.Add(entry.Key, entry.Value);
                        }
                        else
                        {
                            foreach (var posting in entry.Value)
                            {
                                oldFilePostings[posting.Key] = posting.Value;
                            }
                            oldFile.Tokens[entry.Key] = oldFilePostings;
                        }
                    }
                    FieldFiles[oldFileName] = oldFile;
                }
            }
            var rebasedDocFile = new DocFile(rebasedDocs);
            var rebasedDocFileName = Path.GetRandomFileName();
            foreach (var doc in rebasedDocs)
            {
                Dix.DocIdToFileIndex[doc.Key] = rebasedDocFileName;
            }
            DocFiles.Add(Path.Combine(Directory, rebasedDocFileName + ".d"), rebasedDocFile);
        }

        public void Dispose()
        {
        }
    }

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

        protected IEnumerable<string> GetIndexFiles()
        {
            var ids = System.IO.Directory.GetFiles(Directory, "*.ix")
                .Select(f => Int64.Parse(Path.GetFileNameWithoutExtension(f) ?? "-1"))
                .OrderBy(id => id);
            return ids.Select(id => Path.Combine(Directory, id + ".ix"));
        }

        protected IDictionary<string, string> GetDoc(string docId)
        {
            var file = GetDocFile(docId);
            return file.Docs[docId].Fields;
        }

        protected DocFile GetDocFile(string docId)
        {
            return GetDocFile(docId, Dix);
        }

        protected DocFile GetDocFile(string docId, DixFile dix)
        {
            var fileName = Path.Combine(Directory, dix.DocIdToFileIndex[docId] + ".d");
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