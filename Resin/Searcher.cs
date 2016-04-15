using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Searcher : IDisposable
    {
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly Dictionary<string, DocFile> _docFiles;
        private readonly Dictionary<string, FieldFile> _fieldFiles;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly DixFile _dix;
        private readonly FixFile _fix;

        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _docFiles = new Dictionary<string, DocFile>();
            _fieldFiles = new Dictionary<string, FieldFile>();
            _trieFiles = new Dictionary<string, Trie>();
            
            var generations = GetIndexFiles().ToList();
            var firstGen = generations.First();
            var ix = IxFile.Load(Path.Combine(_directory, firstGen));
            _dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
            _fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));

            if (generations.Count > 1)
            {
                Log.DebugFormat("rebasing");
                Rebase(generations.Skip(1));
            }
            Log.DebugFormat("searcher initialized in {0}", _directory);
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
                var ix = IxFile.Load(Path.Combine(_directory, gen));
                foreach (var term in ix.Deletions)
                {
                    var collector = new Collector(_directory, _fix, _fieldFiles, _trieFiles);
                    var docIds = collector.Collect(new QueryContext(term.Field, term.Token), 0, int.MaxValue).Select(ds=>ds.DocId).ToList();
                    foreach (var docId in docIds)
                    {
                        var docFile = GetDocFile(docId);
                        docFile.Docs.Remove(docId);
                        _docFiles[Path.Combine(_directory, _dix.DocIdToFileIndex[docId] + ".d")] = docFile;
                        _dix.DocIdToFileIndex.Remove(docId);

                        foreach (var field in _fix.FieldIndex)
                        {
                            var fileName = Path.Combine(_directory, field.Value + ".f");
                            FieldFile ff;
                            if (!_fieldFiles.TryGetValue(fileName, out ff))
                            {
                                ff = FieldFile.Load(fileName);
                            }
                            ff.Remove(docId);
                            _fieldFiles[fileName] = ff;
                        }
                    }
                }

                var dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
                foreach (var newDoc in dix.DocIdToFileIndex)
                {
                    var docFile = GetDocFile(newDoc.Key, dix);
                    var nd = docFile.Docs[newDoc.Key];
                    if (_dix.DocIdToFileIndex.ContainsKey(newDoc.Key))
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
                        _dix.DocIdToFileIndex[newDoc.Key] = newDoc.Value;
                    }
                    rebasedDocs[nd.Id] = nd;
                }

                var fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));
                foreach (var field in fix.FieldIndex)
                {
                    var newFileName = Path.Combine(_directory, field.Value + ".f");
                    var oldFileName = Path.Combine(_directory, _fix.FieldIndex[field.Key] + ".f");
                    FieldFile oldFile;
                    if (!_fieldFiles.TryGetValue(oldFileName, out oldFile))
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
                    _fieldFiles[oldFileName] = oldFile;
                }
            }
            var rebasedDocFile = new DocFile(rebasedDocs);
            var rebasedDocFileName = Path.GetRandomFileName();
            foreach (var doc in rebasedDocs)
            {
                _dix.DocIdToFileIndex[doc.Key] = rebasedDocFileName;
            }
            _docFiles.Add(Path.Combine(_directory, rebasedDocFileName + ".d"), rebasedDocFile);
        }

        private IEnumerable<string> GetIndexFiles()
        {
            var ids =Directory.GetFiles(_directory, "*.ix")
                .Select(f => Int64.Parse(Path.GetFileNameWithoutExtension(f) ?? "-1"))
                .OrderBy(id => id);
            return ids.Select(id => Path.Combine(_directory, id + ".ix"));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _fix, _fieldFiles, _trieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            var file = GetDocFile(docId);
            return file.Docs[docId].Fields;
        }

        private DocFile GetDocFile(string docId)
        {
            return GetDocFile(docId, _dix);
        }

        private DocFile GetDocFile(string docId, DixFile dix)
        {
            var fileName = Path.Combine(_directory, dix.DocIdToFileIndex[docId] + ".d");
            DocFile file;
            if (!_docFiles.TryGetValue(fileName, out file))
            {
                file = DocFile.Load(fileName);
                _docFiles[fileName] = file;
            }
            return file;
        }

        public void Dispose()
        {
        }
    }
}