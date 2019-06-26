using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Sir.Store;

namespace Sir.HttpServer.Controllers
{
    public class QueryParserController : UIController
    {
        private readonly IQueryFormatter _queryFormatter;
        private readonly PluginsCollection _plugins;
        private readonly IStringModel _model;

        public QueryParserController(
            PluginsCollection plugins,
            IQueryFormatter queryFormatter, 
            IConfigurationProvider config,
            IStringModel tokenizer,
            SessionFactory sessionFactory) : base(config, sessionFactory)
        {
            _queryFormatter = queryFormatter;
            _plugins = plugins;
            _model = tokenizer;
        }

        [HttpGet("/queryparser/")]
        [HttpPost("/queryparser/")]
        public IActionResult Index(string q, string qf, string collection, string newCollection, string[] fields)
        {
            var formatted = qf ?? _queryFormatter.Format(collection, _model, Request);

            ViewData["qf"] = formatted;
            ViewData["q"] = q;

            var reader = _plugins.Get<IReader>("application/json");

            if (reader == null)
            {
                throw new NotSupportedException();
            }

            var timer = new Stopwatch();
            timer.Start();

            var result = reader.Read(collection, _model, Request);

            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["collection"] = collection;
            ViewData["total"] = result.Total;
            ViewData["newCollection"] = newCollection;

            if (result.Total == 0)
            {
                return View(new SearchResultModel[0]);
            }

            var documents = result.Documents.Select(x => new SearchResultModel { Document = x });

            return View(documents);
        }
    }
}