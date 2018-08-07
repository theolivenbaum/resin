using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : Controller
    {
        private PluginsCollection _plugins;
        private Stopwatch _timer = new Stopwatch();

        public SearchController(PluginsCollection plugins)
        {
            _plugins = plugins;
        }

        [HttpGet("/search/")]
        [HttpPost("/search/")]
        public ActionResult Index(string q, string collectionId)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return View("MultilineQuery");
            }

            string collectionName = collectionId ?? "www";

            var htmlEncodedQuery = WebUtility.HtmlEncode(q);
            ViewData["q"] = htmlEncodedQuery;

            if (!q.Contains(":"))
            {
                q = string.Format("title:{0}\nbody:{0}", q);
            }

            _timer.Restart();

            var queryParser = _plugins.Get<IQueryParser>();
            var reader = _plugins.Get<IReader>();
            var tokenizer = _plugins.Get<ITokenizer>();

            if (queryParser == null || reader == null || tokenizer == null)
            {
                throw new System.NotSupportedException();
            }

            var parsedQuery = queryParser.Parse(q, tokenizer);
            parsedQuery.CollectionId = collectionName.ToHash();

            var documents = reader.Read(parsedQuery).Select(x => new SearchResultModel { Document = x }).ToList();

            ViewData["collectionName"] = collectionName;
            ViewData["time_ms"] = _timer.ElapsedMilliseconds;
            ViewBag.Title = htmlEncodedQuery + "Did you gogo?";

            return View(documents);
        }
    }

    public class SearchResultModel
    {
        public IDictionary Document { get; set; }
    }
}