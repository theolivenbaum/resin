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
        private readonly QueryParser _parser;

        private readonly Dictionary<string, FieldFile> _fieldFiles;
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly DocumentReader _docReader;
        private readonly string _directory;
        private readonly FixFile _fix;

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _fieldFiles = new Dictionary<string, FieldFile>();
            _trieFiles = new Dictionary<string, Trie>();

            var ixFileName = Helper.GetFileNameOfLatestIndex(_directory);
            var ix = IxFile.Load(ixFileName);
            var dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
            _fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));
            _docReader = new DocumentReader(_directory, dix);

            Log.DebugFormat("searcher initialized in {0}", _directory);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _fix, _fieldFiles, _trieFiles);
            var scored = collector.Collect(_parser.Parse(query), page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => _docReader.GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }
    }
}