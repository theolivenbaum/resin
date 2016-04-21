using System.Collections.Generic;
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
        private readonly QueryParser _parser;
        private readonly DocumentReader _docReader;
        private readonly Dictionary<string, FieldFile> _fieldFilesByFileId;
        private readonly Dictionary<string, Trie> _triesByFileId;
        private readonly Dictionary<string, List<string>> _fieldToFileIds;

        public Searcher(string directory, QueryParser parser)
        {
            _parser = parser;
            _fieldFilesByFileId = new Dictionary<string, FieldFile>();
            _triesByFileId = new Dictionary<string, Trie>();
            _fieldToFileIds = new Dictionary<string, List<string>>();
            
            var docIdToFileIds = new Dictionary<string, List<string>>();
            var optimizer = new Optimizer(directory, _fieldFilesByFileId, _triesByFileId, _fieldToFileIds, docIdToFileIds);
            optimizer.FastForward();
            _docReader = new DocumentReader(directory, docIdToFileIds);

            Log.DebugFormat("searcher initialized in {0}", directory);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_fieldToFileIds, _fieldFilesByFileId, _triesByFileId);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => _docReader.GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }
    }
}