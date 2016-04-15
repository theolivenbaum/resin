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
        private readonly HashSet<string> _deletedDocs; 
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
            _deletedDocs = new HashSet<string>();
            
            var generations = GetIndexFiles().ToList();
            var firstGen = generations.First();
            var ix = IxFile.Load(Path.Combine(_directory, firstGen));
            _dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
            _fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));

            if (generations.Count > 1) Rebase(generations.Skip(1));
        }

        /// <summary>
        /// Newer generation indices are treated as changesets to older generations.
        /// A changeset (i.e. an index) can contain both (1) document deletions as well as (2) upserts of document fields.
        /// </summary>
        /// <param name="generations">The *.ix file names of the subsequent generations sorted by age, oldest first.</param>
        private void Rebase(IEnumerable<string> generations)
        {
            var rebasedDocs = new Dictionary<string, Document>();
            foreach (var gen in generations.Skip(1))
            {
                var ix = IxFile.Load(Path.Combine(_directory, gen));
                foreach (var term in ix.Deletions)
                {
                    var collector = new Collector(_directory, _fix, _fieldFiles, _trieFiles);
                    var docIds = collector.Collect(new Query(term.Field, term.Token), 0, int.MaxValue).Select(ds=>ds.DocId).ToList();
                    foreach (var id in docIds)
                    {
                        _deletedDocs.Add(id);
                    }
                }
                var dix = DixFile.Load(ix.DixFileName);
                foreach (var newDoc in dix.DocIdToFileIndex)
                {
                    var d = DocFile.Load(Path.Combine(_directory, newDoc.Value + ".d")).Docs[newDoc.Key];
                    if (_dix.DocIdToFileIndex.ContainsKey(newDoc.Key))
                    {
                        var oldDoc = GetDoc(newDoc.Key);
                        foreach (var field in d.Fields)
                        {
                            oldDoc[field.Key] = field.Value;
                        }
                        var rebased = new Document(oldDoc);
                        rebasedDocs.Add(rebased.Id, rebased);
                    }
                    rebasedDocs.Add(d.Id, d);
                }
            }
            var rebasedDocFile = new DocFile(rebasedDocs);
            var rebasedDocFileName = Guid.NewGuid().ToString();
            foreach (var doc in rebasedDocs)
            {
                _dix.DocIdToFileIndex[doc.Key] = rebasedDocFileName;
            }
            _docFiles.Add(rebasedDocFileName, rebasedDocFile);
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
            var scored = collector.Collect(_parser.Parse(query), page, size).Where(ds=>!_deletedDocs.Contains(ds.DocId)).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            // docs shall remain lazy so that further inproc filtering such as Result.Docs.First() will make the query run even faster
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
            var fileName = Path.Combine(_directory, _dix.DocIdToFileIndex[docId] + ".d");
            DocFile file;
            if (!_docFiles.TryGetValue(fileName, out file))
            {
                file = GetDocFile(docId, _dix);
                _docFiles[fileName] = file;
            }
            return file;
        }

        private DocFile GetDocFile(string docId, DixFile dix)
        {
            var fileName = Path.Combine(_directory, dix.DocIdToFileIndex[docId] + ".d");
            var file = DocFile.Load(fileName);
            Log.DebugFormat("cache miss for doc id {0}", docId);
            return file;
        }

        public void Dispose()
        {
        }
    }
}