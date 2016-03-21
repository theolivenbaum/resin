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

        public IEnumerable<Document> Search(string query)
        {
            var terms = _parser.Parse(query).ToList();
            return _reader.GetDocuments(terms); 
        }


        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}