using System;
using System.Diagnostics;
using System.IO;
using log4net;
using Nancy;
using Resin.Analysis;
using Resin.Querying;
using Resin.Sys;

namespace Resin.Host
{
    public class SearchModule : NancyModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (SearchModule));

        public SearchModule()
        {
            Get["/{indexName}/"] = parameters =>
            {
                var indexName = parameters.indexName;
                var query = Request.Query.query;
                var page = (int) Request.Query.page;
                var size = (int) Request.Query.size;
                return HandleRequest(indexName, query, page, size);
            };
        }

        private Result HandleRequest(string indexName, string query, int page, int size)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();
                using (var searcher = GetSearcher(indexName))
                {
                    var result = searcher.Search(query, page, size);

                    Log.InfoFormat("query-exec {0} {1}{2} hit count: {3}", timer.Elapsed, Request.Url.Path, Uri.UnescapeDataString(Request.Url.Query), result.Total);

                    return result; 
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        private Searcher GetSearcher(string name)
        {
            var dir = Path.Combine(Helper.GetDataDirectory(), name);

            return new Searcher(dir, new QueryParser(new Analyzer()), new Tfidf());
        }
    }
}