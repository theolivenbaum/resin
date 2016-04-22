using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    /// <summary>
    /// A reader that provides thread-safe access to an index
    /// </summary>
    public class Searcher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly FixFile _fix;

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _trieFiles = new Dictionary<string, Trie>();
            _fix = FixFile.Load(Path.Combine(_directory, ".fi"));
            Log.DebugFormat("searcher initialized in {0}", directory);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _fix, _trieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            var docIdPart = docId.ToNumericalString();
            var searchPattern = string.Format("{0}.*.do", docIdPart);
            var docFiles = Directory.GetFiles(_directory, searchPattern);
            var doc = new Dictionary<string, string>();
            foreach (var fn in docFiles)
            {
                var file = DocFile.Load(fn);
                var fieldIdentifyer = Path.GetFileNameWithoutExtension(fn).Replace(".do", "").Replace(docIdPart + ".", "");
                var field = _fix.FileIds[fieldIdentifyer];
                doc[field] = file.Value;
            }
            return doc;
        }
    }
}