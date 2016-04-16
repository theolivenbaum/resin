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
        private readonly string _directory;
        private readonly IList<string> _generations;
        private readonly IList<IxFile> _obsoleteIndices;
 
        public Optimizer(string directory, IList<string> generations, DixFile dix, FixFile fix, Dictionary<string, DocFile> docFiles, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles)
            : base(directory, docFiles, fieldFiles, trieFiles)
        {
            _directory = directory;
            _generations = generations;
            _obsoleteIndices = new List<IxFile>();
            Dix = dix;
            Fix = fix;
        }

        public void Rebase()
        {
            Rebase(_generations); 
        }

        public void Save(IxFile ix)
        {
            var fixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".fix");
            var dixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".dix");
            Dix.Save(dixFileName);
            Fix.Save(fixFileName);
            ix.Deletions.Clear();
            ix.DixFileName = dixFileName;
            ix.FixFileName = fixFileName;

            foreach (var f in DocFiles.Values)
            {
                f.Save();
            }
            foreach (var f in FieldFiles.Values)
            {
                f.Save();
            }

            foreach (var x in _obsoleteIndices)
            {
                File.Delete(x.FileName);
            }

            ix.Save();
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
                _obsoleteIndices.Add(ix);
                foreach (var term in ix.Deletions)
                {
                    var collector = new Collector(Directory, Fix, FieldFiles, TrieFiles);
                    var docIds = collector.Collect(new QueryContext(term.Field, term.Token), 0, int.MaxValue).Select(ds => ds.DocId).ToList();

                    // delete docs from doc files and field files
                    // trie files are not touched
                    // loads into memory "old" doc files that have been cleaned
                    // loads into memory all fields except last gen
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

                // upsert docs
                // loads into memory all new doc files
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

                // loads into memory the field files that are appended to in this gen
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

            // rebased docs, those that have been touched since gen 0, are bundled together in a new DocFile
            var rebasedDocFileName = Path.Combine(_directory, Path.GetRandomFileName());
            var rebasedDocFile = new DocFile(rebasedDocFileName, rebasedDocs);
            foreach (var doc in rebasedDocs)
            {
                Dix.DocIdToFileIndex[doc.Key] = rebasedDocFileName;
            }
            DocFiles.Add(Path.Combine(Directory, rebasedDocFileName + ".d"), rebasedDocFile);

            Log.DebugFormat("rebased {0}", _directory);
        }
    }
}