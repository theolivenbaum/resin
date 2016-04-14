using System;
using log4net;

namespace Resin
{
    public class Searcher : IDisposable
    {
        private readonly string _directory;
        private readonly QueryParser _parser;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));

        public Searcher(string directory, QueryParser parser)
        {
            _directory = directory;
            _parser = parser;
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var q = _parser.Parse(query);
            var req = new QueryRequest(_directory);
            return req.GetResult(q, page, size, returnTrace);
        }

        public void Dispose()
        {
        }
    }
}