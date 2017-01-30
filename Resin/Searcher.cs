using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    /// <summary>
    /// A reader that provides thread-safe (but not lock-free) access to an index.
    /// </summary>
    public class Searcher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringScheme _scorer;
        private readonly IndexInfo _ix;
        private readonly Dictionary<string, DocumentReader> _readers;
        private static readonly object Sync = new object();

        public Searcher(string directory, QueryParser parser, IScoringScheme scorer)
        {
            _directory = directory;
            _parser = parser;
            _scorer = scorer;
            _readers = new Dictionary<string, DocumentReader>();

            _ix = IndexInfo.Load(Path.Combine(_directory, "0.ix"));
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var timer = new Stopwatch();
            timer.Start();

            var collector = new Collector(_directory, _ix);
            var q = _parser.Parse(query);

            if (q == null)
            {
                return new Result { Docs = Enumerable.Empty<Document>() };
            }

            Log.DebugFormat("parsed query {0} in {1}", q, timer.Elapsed);

            var scored = collector.Collect(q, _scorer).ToList();
            var skip = page * size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var docs = paged.Values.Select(s => GetDoc(s.DocId));

            return new Result { Docs = docs, Total = scored.Count };  
        }

        private Document GetDoc(string docId)
        {
            var fileId = docId.ToDocFileId();
            DocumentReader reader;
            if (!_readers.TryGetValue(fileId, out reader))
            {
                lock (Sync)
                {
                    if (!_readers.TryGetValue(fileId, out reader))
                    {
                        reader = new DocumentReader(_directory, fileId);
                        _readers.Add(fileId, reader);
                    }
                }
            }
            return reader.Get(docId);
        }
    }
}