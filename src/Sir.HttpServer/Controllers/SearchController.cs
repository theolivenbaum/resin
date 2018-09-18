using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : UIController
    {
        private readonly PluginsCollection _plugins;
        private readonly CrawlQueue _crawlQueue;

        public SearchController(PluginsCollection plugins, CrawlQueue crawlQueue)
        {
            _plugins = plugins;
            _crawlQueue = crawlQueue;
        }

        [HttpGet("/search/")]
        [HttpPost("/search/")]
        public ActionResult Index(string q, string cid)
        {
            if (string.IsNullOrWhiteSpace(q)) return View();

            string query = q.Trim().Replace(":", string.Empty);
            string collectionId = cid ?? "www";
            string htmlEncodedQuery = WebUtility.HtmlEncode(query);

            ViewData["q"] = query;

            var expandedQuery = string.Format("title:{0}\nbody:{0}", query);

            var timer = new Stopwatch();
            timer.Start();

            var queryParser = _plugins.Get<IQueryParser>();
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>();

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new System.NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(expandedQuery, tokenizer);
            parsedQuery.CollectionId = collectionId.ToHash();

            long total;
            var documents = reader.Read(parsedQuery, out total)
                .Select(x => new SearchResultModel { Document = x })
                .Take(100);



            ViewData["collectionName"] = collectionId;
            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["last_processed_url"] = _crawlQueue.LastProcessed.uri;
            ViewData["last_processed_title"] = _crawlQueue.LastProcessed.title;
            ViewData["total"] = total;

            return View(documents);
        }
    }

    public class SearchResultModel
    {
        public IDictionary Document { get; set; }
    }
}