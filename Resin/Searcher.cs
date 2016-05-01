using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly IScoringScheme _scorer;
        private readonly ConcurrentDictionary<string, LazyTrie> _trieFiles;
        private readonly IxInfo _ix;
        //private readonly ConcurrentDictionary<string, PostingsContainer> _postingsCache;
        private readonly ConcurrentDictionary<string, Document> _docCache;
 
        public Searcher(string directory, QueryParser parser, IScoringScheme scorer)
        {
            _directory = directory;
            _parser = parser;
            _scorer = scorer;
            _trieFiles = new ConcurrentDictionary<string, LazyTrie>();
            //_postingsCache = new ConcurrentDictionary<string, PostingsContainer>();
            _docCache = new ConcurrentDictionary<string, Document>();

            _ix = IxInfo.Load(Path.Combine(_directory, "0.ix"));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var timer = new Stopwatch();
            var collector = new Collector(_directory, _ix, _trieFiles, new ConcurrentDictionary<string, PostingsContainer>());
            timer.Start();
            var q = _parser.Parse(query);
            if (q == null)
            {
                return new Result{Docs = Enumerable.Empty<IDictionary<string, string>>()};
            }
            Log.DebugFormat("parsed query {0} in {1}", q, timer.Elapsed);
            var scored = collector.Collect(q, page, size, _scorer).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count};
        }

        //private IDictionary<string, string> GetDoc(string docId)
        //{
        //    Document doc;
        //    if (!_docCache.TryGetValue(docId, out doc))
        //    {
        //        var bucketId = docId.ToDocBucket();
        //        var fileName = Path.Combine(_directory, bucketId + ".dl");
        //        var container = DocContainer.Load(fileName);
        //        doc = container.Get(docId, _directory);
        //        _docCache[docId] = doc;
        //    }
        //    return doc.Fields;
        //}

        private IDictionary<string, string> GetDoc(string docId)
        {
            var bucketId = docId.ToDocBucket();
            var fileName = Path.Combine(_directory, bucketId + ".dl");
            var container = DocContainer.Load(fileName);
            return container.Get(docId, _directory).Fields;
        }
    }
}