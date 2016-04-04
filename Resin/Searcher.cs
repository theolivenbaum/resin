using System;
using System.Collections.Generic;
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
            var expandedTerms = new List<Term>();
            foreach (var term in terms)
            {
                expandedTerms.AddRange(_reader.FieldScanner.Expand(term));
            }
            var scored = _reader.GetScoredResult(expandedTerms).OrderByDescending(d => d.Score).ToList();
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score);
            var docs = paged.Values.Select(s=>_cacheDocs? _reader.GetDoc(s) : _reader.GetDocNoCache(s));
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }
        
        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}