using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Sir.HttpServer.Controllers
{
    public class QueryParserController : UIController
    {
        private IServiceProvider _serviceProvider;

        public QueryParserController(IServiceProvider serviceProvider, IConfigurationProvider config) : base(config)
        {
            _serviceProvider = serviceProvider;
        }

        [HttpGet("/queryparser/")]
        [HttpPost("/queryparser/")]
        public IActionResult Index(string q, string qf, string collection, string[] fields)
        {
            var formatter = _serviceProvider.GetService<IQueryFormatter>();
            var formatted = qf ?? formatter.Format(collection, Request);

            ViewData["q"] = q;
            ViewData["qf"] = formatted;

            return View(new QueryParserModel[0]);

            //var reader = _plugins.Get<IReader>("application/json");

            //if (reader == null)
            //{
            //    throw new System.NotSupportedException();
            //}

            //var timer = new Stopwatch();
            //timer.Start();

            //var result = await reader.Read(collection, Request);

            //ViewData["time_ms"] = timer.ElapsedMilliseconds;
            //ViewData["collection"] = collection;
            //ViewData["total"] = result.Total;

            //if (result.Total== 0)
            //{
            //    return View(new QueryParserModel[0]);
            //}

            //var documents = result.Documents.Select(x => new QueryParserModel { Document = x });

            //return View(documents);
        }
    }

    public class QueryParserModel
    {
        public IDictionary Document { get; set; }
    }
}