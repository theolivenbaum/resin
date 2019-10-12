using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : UIController
    {
        private readonly PluginsCollection _plugins;
        private readonly IStringModel _model;

        public SearchController(
            PluginsCollection plugins, 
            IConfigurationProvider config, 
            IStringModel tokenizer,
            ISessionFactory sessionFactory) : base(config, sessionFactory)
        {
            _plugins = plugins;
            _model = tokenizer;
        }

        [HttpGet("/search/")]
        [HttpPost("/search/")]
        public IActionResult Index(string q, string collection)
        {
            if (string.IsNullOrWhiteSpace(q)) return View();

            ViewData["q"] = q;

            var reader = _plugins.Get<IHttpReader>("application/json");

            if (reader == null)
            {
                throw new System.NotSupportedException();
            }

            var timer = new Stopwatch();
            timer.Start();

            var result = reader.Read(collection, _model, Request);

            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["total"] = result.Total;

            if (result.Total == 0)
            {
                return View(new SearchResultModel[0]);
            }

            var documents = result.Documents.Select(x => new SearchResultModel { Document = x });

            return View(documents);
        }
    }

    public class SearchResultModel
    {
        public IDictionary<string, object> Document { get; set; }
    }
}