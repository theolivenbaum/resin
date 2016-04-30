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
        private readonly ConcurrentDictionary<string, LazyTrie> _trieFiles;
        private readonly IxFile _ix;
        private readonly ConcurrentDictionary<string, DocContainerFile> _docCache;
        private readonly ConcurrentDictionary<string, PostingsContainerFile> _postingsCache; 
 
        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
            _trieFiles = new ConcurrentDictionary<string, LazyTrie>();
            _docCache = new ConcurrentDictionary<string, DocContainerFile>();
            _postingsCache = new ConcurrentDictionary<string, PostingsContainerFile>();
            _ix = IxFile.Load(Path.Combine(_directory, "0.ix"));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var collector = new Collector(_directory, _ix, _trieFiles, _postingsCache);
            var q = _parser.Parse(query);
            if (q == null)
            {
                return new Result{Docs = Enumerable.Empty<IDictionary<string, string>>()};
            }
            var scored = collector.Collect(q, page, size).ToList();
            var skip = page*size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var docs = paged.Values.Select(s => GetDoc(s.DocId)); 
            return new Result { Docs = docs, Total = scored.Count};
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            var bucketId = docId.ToDocBucket();
            DocContainerFile container;
            if (!_docCache.TryGetValue(bucketId, out container))
            {
                var fileName = Path.Combine(_directory, bucketId + ".dl");
                container = DocContainerFile.Load(fileName);
                _docCache[bucketId] = container;
            }
            return container.Files[docId].Fields;
        }
    }
}