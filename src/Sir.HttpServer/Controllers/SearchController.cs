using System;
using System.Diagnostics;
using System.IO;
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
        private readonly IConfigurationProvider _config;

        public SearchController(
            IHttpReader reader, 
            IConfigurationProvider config,
            IModel<string> model,
            Database sessionFactory) : base(config, sessionFactory)
        {
            _reader = reader;
            _model = model;
            _config = config;
        }

        [HttpGet("/search/")]
        [HttpPost("/search/")]
        public async Task<IActionResult> Index(string q, string queryId)
        {
            if (string.IsNullOrWhiteSpace(queryId)) return NotFound();

            var userDirectory = Path.Combine(_config.Get("user_dir"), queryId);
            var fileName = Path.Combine(userDirectory, $"{"url".ToHash()}.docs");

            if (System.IO.File.Exists(fileName))
            {
                var fi = new FileInfo(fileName);
                var created = fi.CreationTimeUtc;
                var age = (int)DateTime.Now.Subtract(created).TotalDays;
                var expiresInDays = 30 - age;

                ViewData["index_expires_in_days"] = expiresInDays;
            }
            else
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(q)) return View();

            var timer = Stopwatch.StartNew();

            ViewData["q"] = q;

            var result = await _reader.Read(Request, _model);

            ViewData["time_ms"] = timer.ElapsedMilliseconds;
            ViewData["total"] = result.Total;

            return View(result);
        }
    }
}