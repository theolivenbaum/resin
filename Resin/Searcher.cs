using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    /// <summary>
    /// A reader that provides thread-safe access to an index
    /// </summary>
    public class Searcher
    {
        //private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly IxFile _ix;
        private readonly ConcurrentDictionary<string, Document> _docCache;
 
        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _trieFiles = new Dictionary<string, Trie>();
            _docCache = new ConcurrentDictionary<string, Document>();

            var fileName = Path.Combine(_directory, "0.ix");
            if (!File.Exists(fileName)) throw new ArgumentException(string.Format("No index found in {0}", _directory), "directory");
            _ix = IxFile.Load(Path.Combine(_directory, "0.ix"));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _ix, _trieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            Document doc;
            if (!_docCache.TryGetValue(docId, out doc))
            {
                var containerId = docId.ToDocHash();
                var fileName = Path.Combine(_directory, containerId + ".dl");
                var file = DocContainerFile.Load(fileName);
                doc = file.Files[docId];
                _docCache[docId] = doc;
            }
            return doc.Fields;
        }
    }
}