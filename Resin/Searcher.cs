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
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly Dictionary<string, DocFile> _docFiles;
        private readonly Dictionary<string, FieldFile> _fieldFiles;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly DixFile _dix;
        private readonly FixFile _fix;

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _docFiles = new Dictionary<string, DocFile>();
            _fieldFiles = new Dictionary<string, FieldFile>();
            _trieFiles = new Dictionary<string, Trie>();

            var ix = IxFile.Load(Path.Combine(directory, "0.ix"));
            _dix = DixFile.Load(Path.Combine(directory, ix.DixFileName));
            _fix = FixFile.Load(Path.Combine(directory, ix.FixFileName));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _fix, _fieldFiles, _trieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size, returnTrace).ToList();
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
            var fileName = Path.Combine(_directory, _dix.DocIdToFileIndex[docId] + ".d");
            DocFile file;
            if (!_docFiles.TryGetValue(fileName, out file))
            {
                file = DocFile.Load(fileName);
                Log.DebugFormat("fetched from disk: doc {0}", docId);
                _docFiles[fileName] = file;
            }
            return file;
        }

        public void Dispose()
        {
        }
    }
}