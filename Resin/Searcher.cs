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
        private readonly IxFile _ix;
        private readonly DixFile _dix;

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _docFiles = new Dictionary<string, DocFile>();
            _fieldFiles = new Dictionary<string, FieldFile>();
            _ix = IxFile.Load(Path.Combine(directory, "0.ix"));
            _dix = DixFile.Load(Path.Combine(directory, _ix.DixFileName));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var q = _parser.Parse(query);
            var req = new QueryRequest(_directory, _ix.FixFileName, _fieldFiles);
            var scored = req.GetResult(q, page, size, returnTrace).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)).ToList();
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