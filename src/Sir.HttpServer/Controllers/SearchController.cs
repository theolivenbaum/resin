using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> Index(string q, string cid)
        {
            if (string.IsNullOrWhiteSpace(q)) return View();

            string query = q.Trim().Replace(":", string.Empty);
            string collectionId = cid ?? "www";
            string htmlEncodedQuery = WebUtility.HtmlEncode(query);

            ViewData["q"] = query;

            var expandedQuery = string.Format("title:{0}\nbody:{0}", query);

            var timer = new Stopwatch();
            timer.Start();

            var mediaType = Request.Headers["Accept"].ToArray()[0];
            var reader = _plugins.Get<IReader>(mediaType);

            if (reader == null)
            {
                throw new System.NotSupportedException();
            }

            var result = await reader.Read(collectionId, Request);
            var documents = result.Documents
                .Select(x => new SearchResultModel { Document = x })
                .Take(100);

            ViewData["collectionName"] = collectionId;
            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["total"] = result.Total;

            return View(documents);
        }
    }

    public class SearchResultModel
    {
        public IDictionary Document { get; set; }
    }
}