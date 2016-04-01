using System;
using System.Linq;
using log4net;

namespace Resin
{
    public class Searcher : IDisposable
    {
        private readonly QueryParser _parser;
        private readonly bool _cacheDocs;
        private readonly IndexReader _reader;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexReader));

        public IndexReader Reader { get { return _reader; } }

        public Searcher(string dir, bool cacheDocs = true) : this(new IndexReader(dir), new QueryParser(new Analyzer()), cacheDocs){}

        public Searcher(IndexReader reader, QueryParser parser, bool cacheDocs = true)
        {
            _reader = reader;
            _parser = parser;
            _cacheDocs = cacheDocs;

            Log.InfoFormat("searcher init with doc cache {0}", cacheDocs ? "on" : "off");
        }

        public Result Search(string query, int page = 0, int size = 10000)
        {
            var skip = page*size;
            var terms = _parser.Parse(query);
            var scored = _reader.GetScoredResult(terms).OrderByDescending(d => d.Score).ToList();
            var paged = scored.Skip(skip).Take(size);
            var docs = paged.Select(s=>_cacheDocs? _reader.GetDoc(s) : _reader.GetDocNoCache(s));
            return new Result { Docs = docs, Total = scored.Count };
        }
        
        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}