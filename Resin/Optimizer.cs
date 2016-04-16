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
        private readonly IList<string> _obsoleteIndices;
 
        public Optimizer(string directory, IList<string> generations, DixFile dix, FixFile fix, Dictionary<string, DocFile> docFiles, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles)
            : base(directory, docFiles, fieldFiles, trieFiles)
        {
            _directory = directory;
            _generations = generations;
            _obsoleteIndices = new List<string>();
            Dix = dix;
            Fix = fix;
        }

        public void Rebase()
        {
            Rebase(_generations.Skip(1).ToList()); 
        }

        public void Save(IxFile ix)
        {
            var fixFileId = Path.GetRandomFileName() + ".fix";
            var dixFileId = Path.GetRandomFileName() + ".dix";
            var fixFileName = Path.Combine(_directory, fixFileId);
            var dixFileName = Path.Combine(_directory, dixFileId);
            Dix.Save(dixFileName);
            Fix.Save(fixFileName);
            ix.Deletions.Clear();
            ix.DixFileName = dixFileId;
            ix.FixFileName = fixFileId;

            foreach (var d in DocFiles)
            {
                d.Value.Save(Path.Combine(_directory, d.Key + ".d"));
            }
            foreach (var f in FieldFiles)
            {
                f.Value.Save(Path.Combine(_directory, f.Key + ".f"));
            }
            foreach (var x in _obsoleteIndices)
            {
                File.Delete(x);
            }

            ix.Save(_generations.First());
            Log.DebugFormat("optimized {0}", _directory);
        }

        /// <summary>
        /// Newer generation indices are treated as changesets to older generations.
        /// A changeset (i.e. an index) can contain both (1) document deletions as well as (2) upserts of document fields.
        /// </summary>
        /// <param name="generations">The *.ix file names of subsequent generations sorted by age, oldest first.</param>
        private void Rebase(IList<string> generations)
        {
            if (generations.Count < 1) return;
            var rebasedDocs = new Dictionary<string, Document>();
            foreach (var gen in generations)
            {
                var ix = IxFile.Load(gen);
                _obsoleteIndices.Add(gen);
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
                        Dix.DocIdToFileId.Remove(docId);

                        foreach (var field in Fix.FieldToFileId)
                        {
                            var fileId = field.Value;
                            var fileName = Path.Combine(Directory, fileId + ".f");
                            FieldFile ff;
                            if (!FieldFiles.TryGetValue(fileId, out ff))
                            {
                                ff = FieldFile.Load(fileName);
                            }
                            ff.Remove(docId);
                            FieldFiles[fileId] = ff;
                        }
                    }
                }

                // upsert docs
                // loads into memory all upserted docs
                var dix = DixFile.Load(Path.Combine(Directory, ix.DixFileName));
                foreach (var fileId in dix.DocIdToFileId.Values.Distinct())
                {
                    var docFile = DocFile.Load(Path.Combine(_directory, fileId + ".d"));
                    foreach (var newDoc in docFile.Docs.Values)
                    {
                        if (Dix.DocIdToFileId.ContainsKey(newDoc.Id))
                        {
                            var oldDoc = GetDoc(newDoc.Id);
                            foreach (var field in newDoc.Fields)
                            {
                                oldDoc[field.Key] = field.Value; // upsert of field
                            }
                            rebasedDocs[newDoc.Id] = new Document(oldDoc);
                        }
                        else
                        {
                            rebasedDocs[newDoc.Id] = newDoc;
                        }
                    }
                }
                

                // remove stale data from field files
                // append new data
                // loads into memory the field files that are updated by this gen
                var fix = FixFile.Load(Path.Combine(Directory, ix.FixFileName));
                foreach (var field in fix.FieldToFileId)
                {
                    var newFileId = field.Value;
                    if (Fix.FieldToFileId.ContainsKey(field.Key))
                    {
                        // we already know about this field
                        // load into memory the old field file
                        // remove stale data
                        // add new data

                        var oldFileId = Fix.FieldToFileId[field.Key];
                        var newFileName = Path.Combine(Directory, newFileId + ".f");
                        var oldFileName = Path.Combine(Directory, oldFileId + ".f");
                        FieldFile oldFile;
                        if (!FieldFiles.TryGetValue(oldFileId, out oldFile))
                        {
                            oldFile = FieldFile.Load(oldFileName);
                        }
                        var newFile = FieldFile.Load(newFileName);
                        
                        // remove stale postings
                        foreach (var docId in newFile.DocIds.Keys)
                        {
                            oldFile.Remove(docId);
                        }

                        // add new postings
                        foreach (var newTermPostings in newFile.Tokens)
                        {
                            Dictionary<string, int> oldFilePostings;
                            if (!oldFile.Tokens.TryGetValue(newTermPostings.Key, out oldFilePostings))
                            {
                                // I had not heard of that word before

                                oldFile.Tokens.Add(newTermPostings.Key, newTermPostings.Value);
                                foreach (var docId in newTermPostings.Value.Keys)
                                {
                                    oldFile.DocIds[docId] = null;
                                }
                            }
                            else
                            {
                                // seen that word before

                                foreach (var posting in newTermPostings.Value)
                                {
                                    oldFilePostings[posting.Key] = posting.Value;
                                    oldFile.DocIds[posting.Key] = null;
                                }
                                oldFile.Tokens[newTermPostings.Key] = oldFilePostings;
                            }
                        }
                        FieldFiles[oldFileId] = oldFile;
                    }
                    else
                    {
                        // completely new field
                        Fix.FieldToFileId[field.Key] = newFileId;
                    }
                }
            }

            // rebased docs, those that have been touched or added since gen 0, are bundled together in a new DocFile
            // TODO: use a docwriter?
            var docFileId = Path.GetRandomFileName();
            var rebasedDocFile = new DocFile(rebasedDocs);
            foreach (var doc in rebasedDocs.Values)
            {
                Dix.DocIdToFileId[doc.Id] = docFileId;
            }
            DocFiles.Add(docFileId, rebasedDocFile);

            Log.DebugFormat("rebased {0}", _directory);
        }
    }
}