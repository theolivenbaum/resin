using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Diagnostics;

namespace Resin.DocumentSearchServer
{
    public class SearchController : Controller
    {
        public IActionResult Search(string q = null)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(new
                {
                    message = "This is Resin Document Search Server.",
                    usage = string.Format("{0}/search/?q=url_encoded_query (RQL)", Request.Host)
                });
            }

            var timer = new Stopwatch();
            timer.Start();

            var query = PreParseQuery(q);
            var dataDir = Directory.GetCurrentDirectory();

            var result = DoSearch(query, dataDir);

            var model = new SearchPage
            {
                Query = q,
                ResultInfo = new ResultInfo
                {
                    Total = result.Total,
                    Count = result.Docs.Count,
                    Elapsed = timer.Elapsed
                },
                SearchHits = result.Docs.ToSearchHits()
            };

            return Json(model);
        }

        private string PreParseQuery(string raw)
        {
            if (raw.Contains(":") && !raw.StartsWith(":"))
            {
                return raw;
            }

            var cleaned = raw.Replace(":", "").Replace("~", "").Replace("*", "");

            return string.Format("body:{0} title:{0}", cleaned);
        }

        private ScoredResult DoSearch(string query, string dataDirectory)
        {
            return Program.Searcher.Search(query, 0, 15);
        }
    }
}