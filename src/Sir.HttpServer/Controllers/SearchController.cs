using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : UIController
    {
        private readonly PluginsCollection _plugins;

        public SearchController(PluginsCollection plugins)
        {
            _plugins = plugins;
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

            var queryParser = _plugins.Get<IHttpQueryParser>();
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>();

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new System.NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(collectionId, Request, tokenizer);
            var result = reader.Read(parsedQuery);
            var documents = (IEnumerable<IDictionary>)result.Data;

            ViewData["collectionName"] = collectionId;
            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["last_processed_url"] = _crawlQueue.LastProcessed.uri;
            ViewData["last_processed_title"] = _crawlQueue.LastProcessed.title;
            ViewData["total"] = result.Total;

            return View(documents);
        }
    }
}