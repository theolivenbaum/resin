using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using Sir.VectorSpace;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : UIController
    {
        private readonly IHttpReader _reader;
        private readonly IModel<string> _model;

        public SearchController(
            IHttpReader reader, 
            IConfigurationProvider config,
            IModel<string> model,
            Database sessionFactory) : base(config, sessionFactory)
        {
            _reader = reader;
            _model = model;
        }

        [HttpGet("/search/")]
        [HttpPost("/search/")]
        public async Task<IActionResult> Index(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return View();

            var timer = new Stopwatch();
            timer.Start();

            ViewData["q"] = q;

            var result = await _reader.Read(Request, _model);

            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["total"] = result.Total;

            return View(result);
        }
    }
}