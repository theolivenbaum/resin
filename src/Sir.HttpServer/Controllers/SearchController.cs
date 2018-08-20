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
            ViewData["last_processed_url"] = _crawlQueue.LastProcessed.uri;
            ViewData["last_processed_title"] = _crawlQueue.LastProcessed.title;

            if (string.IsNullOrWhiteSpace(q))
            {
                return View("MultilineQuery");
            }

            var isMultiline = q.Contains('\n');
            string collectionId = cid ?? "www";
            string htmlEncodedQuery = WebUtility.HtmlEncode(q);

            ViewData["q"] = q;

            if (!q.Contains(":"))
            {
                q = string.Format("title:{0}\nbody:{0}\nurl:{0}", q);
            }

            var timer = new Stopwatch();
            timer.Start();

            var queryParser = _plugins.Get<IQueryParser>();
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>();

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new System.NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(q, tokenizer);
            parsedQuery.CollectionId = collectionId.ToHash();

            var documents = reader.Read(parsedQuery)
                .GroupBy(x => x["_url"])
                .SelectMany(x => x.OrderByDescending(y=>y["_created"]).Take(1))
                .Select(x => new SearchResultModel { Document = x }).ToList();

            ViewData["collectionName"] = collectionId;
            ViewData["time_ms"] = timer.ElapsedMilliseconds;

            return isMultiline ? View("MultilineQuery", documents) : View(documents);
        }
    }

    public class SearchResultModel
    {
        public IDictionary Document { get; set; }
    }
}