using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using Nancy;

namespace Resin
{
    public class SearchModule : NancyModule
    {
        private static readonly IDictionary<string, Searcher> Searchers = new Dictionary<string, Searcher>();
        private static readonly object Sync = new object();
        private static readonly ILog Log = LogManager.GetLogger(typeof(SearchModule));

        public SearchModule()
        {
            Get["/{indexName}/"] = parameters =>
            {
                var timer = new Stopwatch();
                timer.Start();
                var indexName = parameters.indexName;
                var query = Request.Query.query;
                var page = (int) Request.Query.page;
                var size = (int)Request.Query.size;
                return HandleRequest(indexName, query, page, size);
            };
        }

        private ResolvedResult HandleRequest(string indexName, string query, int page, int size)
        {
            var timer = new Stopwatch();
            timer.Start();
            var searcher = GetSearcher(indexName);
            var lazyResult = searcher.Search(query, page, size);
            var resolved = lazyResult.Resolve();
            Log.InfoFormat("query-exec {0} {1}{2} hits-total {3}", timer.Elapsed, Request.Url.Path, Uri.UnescapeDataString(Request.Url.Query), resolved.Total);
            return resolved;
        }

        private Searcher GetSearcher(string name)
        {
            var dir = Path.Combine(GetBaseFolder(), name);
            Searcher searcher;
            if (!Searchers.TryGetValue(dir, out searcher))
            {
                lock (Sync)
                {
                    if (!Searchers.TryGetValue(dir, out searcher))
                    {
                        searcher = new Searcher(dir);
                        Searchers.Add(dir, searcher);
                    }
                }
            }
            return searcher;           
        }

        private static string GetBaseFolder()
        {
            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            return Path.Combine(path, "Resin");
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