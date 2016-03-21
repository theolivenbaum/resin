using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class Searcher : IDisposable
    {
        private readonly QueryParser _parser;
        private readonly IndexReader _reader;

        public IndexReader Reader { get { return _reader; } }

        public Searcher(string dir)
            : this(new IndexReader(new Scanner(dir)), new QueryParser(new Analyzer()))
        {}

        public Searcher(IndexReader reader, QueryParser parser)
        {
            _reader = reader;
            _parser = parser;
        }

        public Result Search(string query, int page = 0, int size = 20)
        {
            var skip = page*size;
            var terms = _parser.Parse(query);
            var scored = _reader.GetScoredResult(terms).ToList();
            var paged = scored.Skip(skip).Take(size);
            var docs = paged.Select(s=>_reader.GetDocFromDisk(s));
            return new Result { Docs = docs, Total = scored.Count };
        }


        public void Dispose()
        {
            _reader.Dispose();
        }
    }

    public class Result
    {
        public IEnumerable<Document> Docs { get; set; }
        public int Total { get; set; }
    }
}