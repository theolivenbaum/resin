using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Optimizer : DocumentReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Optimizer));
        private readonly string _directory;
        private readonly IList<string> _generations;

        protected readonly Dictionary<string, FieldFile> FieldFiles;
        protected readonly Dictionary<string, Trie> TrieFiles;

        public Optimizer(
            string directory, 
            IList<string> generations, 
            DixFile dix, 
            FixFile fix, 
            Dictionary<string, DocFile> docFiles, 
            Dictionary<string, FieldFile> fieldFiles, 
            Dictionary<string, Trie> trieFiles, 
            Dictionary<string, Document> docs) : base(directory, dix, docFiles, docs)
        {
            FieldFiles = fieldFiles;
            TrieFiles = trieFiles;
            _directory = directory;
            _generations = generations;
            Fix = fix;
        }

        public void Rebase()
        {
            if (_generations.Count < 2) return;
            Rebase(_generations.Skip(1).ToList()); 
        }

        public void Save(IxFile ix)
        {
            var docWriter = new DocumentWriter(_directory, Docs);
            docWriter.Flush(Dix);
            foreach (var fieldFile in FieldFiles)
            {
                var fileId = fieldFile.Key;
                fieldFile.Value.Save(Path.Combine(_directory, fileId + ".f"));
                TrieFiles[fileId].Save(Path.Combine(_directory, fileId + ".f.tri"));
            }
            foreach (var x in _generations)
            {
                File.Delete(x);
            }
            var fixFileId = Path.GetRandomFileName() + ".fix";
            var dixFileId = Path.GetRandomFileName() + ".dix";
            var fixFileName = Path.Combine(_directory, fixFileId);
            var dixFileName = Path.Combine(_directory, dixFileId);
            Dix.Save(dixFileName);
            Fix.Save(fixFileName);
            ix.Deletions.Clear();
            ix.DixFileName = dixFileId;
            ix.FixFileName = fixFileId;
            var ixFileName = Helper.GetChronologicalIndexFileName(_directory); //TODO: the timing is fucked up
            ix.Save(ixFileName);
            Log.DebugFormat("saved new index {0}", ixFileName);
        }

        /// <summary>
        /// Rebasing loads all files into memory that have been touched in a write since last time the index was optimized.
        /// Newer generation indices are treated as changesets to older generations.
        /// An index can contain both document deletions as well as upserts of documents.
        /// </summary>
        /// <param name="generations">The *.ix file names of subsequent generations sorted by age, oldest first.</param>
        private void Rebase(IList<string> generations)
        {
            if (generations.Count < 1) return;

            Log.InfoFormat("rebasing {0}", _directory);
            var timer = new Stopwatch();
            timer.Start();
            foreach (var gen in generations)
            {
                var ix = IxFile.Load(gen);
                foreach (var term in ix.Deletions)
                {
                    var collector = new Collector(Directory, Fix, FieldFiles, TrieFiles);
                    var docIds = collector.Collect(new QueryContext(term.Field, term.Token), 0, int.MaxValue).Select(ds => ds.DocId).ToList();

                    // delete docs
                    // loads into memory all cleaned doc files
                    // loads all field files except last gen
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
                                FieldFiles[fileId] = ff;
                            }
                            ff.Remove(docId);
                        }
                    }
                }

                // upsert docs
                // loads all updated docs
                var dix = DixFile.Load(Path.Combine(Directory, ix.DixFileName));
                foreach (var fileId in dix.DocIdToFileId.Values.Distinct())
                {
                    var fileName = Path.Combine(_directory, fileId + ".d");
                    var file = DocFile.Load(fileName);
                    foreach (var doc in file.Docs)
                    {
                        if (Dix.DocIdToFileId.ContainsKey(doc.Key))
                        {
                            var prevDoc = GetDoc(doc.Key);
                            foreach (var field in doc.Value.Fields)
                            {
                                prevDoc[field.Key] = field.Value; // field-wise upsert
                            }
                            Docs[doc.Key] = new Document(prevDoc);
                        }
                        else
                        {
                            Dix.DocIdToFileId[doc.Key] = null; // value will be set when saving
                            Docs[doc.Key] = doc.Value;
                        }
                    }
                }

                // update field files
                // loads into memory all updated field files
                var fix = FixFile.Load(Path.Combine(Directory, ix.FixFileName));
                foreach (var field in fix.FieldToFileId)
                {
                    var fieldName = field.Key;
                    var newFileId = field.Value;
                    var newFileName = Path.Combine(Directory, newFileId + ".f");
                    var newFile = FieldFile.Load(newFileName);

                    if (Fix.FieldToFileId.ContainsKey(fieldName))
                    {
                        // we already know about this field

                        var previousFileId = Fix.FieldToFileId[fieldName];
                        var previousFileName = Path.Combine(Directory, previousFileId + ".f");
                        
                        FieldFile prevFieldFile;
                        if (!FieldFiles.TryGetValue(previousFileId, out prevFieldFile))
                        {
                            prevFieldFile = FieldFile.Load(previousFileName);
                            FieldFiles[previousFileId] = prevFieldFile;
                        }

                        Trie prevTri;
                        if (!TrieFiles.TryGetValue(previousFileId, out prevTri))
                        {
                            prevTri = Trie.Load(previousFileName + ".tri");
                            TrieFiles[previousFileId] = prevTri;
                        }

                        foreach (var entry in newFile.Entries)
                        {
                            var token = entry.Key;
                            foreach (var posting in entry.Value)
                            {
                                var docId = posting.Key;
                                var freq = posting.Value;
                                prevFieldFile.AddOrOverwrite(docId, token, freq);
                            }
                            prevTri.Add(token);
                        }

                        var rebasedFileId = Path.GetRandomFileName();

                        Fix.FieldToFileId[fieldName] = rebasedFileId;
                        FieldFiles[rebasedFileId] = prevFieldFile;
                        TrieFiles[rebasedFileId] = prevTri;
                    }
                    else
                    {
                        // completely new field
                        Fix.FieldToFileId[fieldName] = newFileId;
                        FieldFiles[newFileId] = newFile;
                        TrieFiles[newFileId] = Trie.Load(newFileName + ".tri");
                    }
                }
            }
            Log.InfoFormat("rebased {0} in {1}", _directory, timer.Elapsed);
        }
    }
}