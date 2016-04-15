using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using Nancy;

namespace Resin.Host
{
    public class SearchModule : NancyModule
    {
        private static readonly IDictionary<string, Searcher> Searchers = new Dictionary<string, Searcher>();
        private static readonly object Sync = new object();
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

        private ResolvedResult HandleRequest(string indexName, string query, int page, int size)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();
                var searcher = GetSearcher(indexName);
                var lazyResult = searcher.Search(query, page, size);
                var resolved = lazyResult.Resolve();
                Log.InfoFormat("query-exec {0} {1}{2} hits-total {3}", timer.Elapsed, Request.Url.Path, Uri.UnescapeDataString(Request.Url.Query), resolved.Total);
                return resolved;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        private Searcher GetSearcher(string name)
        {
            var dir = Path.Combine(Helper.GetResinDataDirectory(), name);
            Searcher searcher;
            if (!Searchers.TryGetValue(dir, out searcher))
            {
                lock (Sync)
                {
                    if (!Searchers.TryGetValue(dir, out searcher))
                    {
                        searcher = new Searcher(dir, new QueryParser(new Analyzer()));
                        Searchers.Add(dir, searcher);
                    }
                }
            }
            return searcher;
        }

        public static void ReleaseCache()
        {
            lock (Sync)
            {
                Searchers.Clear();
            }
        }
    }
}