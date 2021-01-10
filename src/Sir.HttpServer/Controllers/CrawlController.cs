using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;
using Sir.Search;
using System;
using Sir.VectorSpace;

namespace Sir.HttpServer.Controllers
{
    [Route("crawl")]
    public class CrawlController : UIController
    {
        private readonly JobQueue _queue;
        private readonly IModel<string> _model;
        private readonly QueryParser<string> _queryParser;
        private readonly ILogger<CrawlController> _log;
        private readonly IConfigurationProvider _config;

        public CrawlController(
            IConfigurationProvider config,
            StreamFactory sessionFactory,
            IModel<string> model,
            QueryParser<string> queryParser,
            JobQueue queue,
            ILogger<CrawlController> log) : base(config, sessionFactory)
        {
            _queue = queue;
            _model = model;
            _queryParser = queryParser;
            _log = log;
            _config = config;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.CrawlId = Guid.NewGuid().ToString();

            return View();
        }

        [HttpPost]
        public IActionResult Post(
            string crawlid, 
            string[] collection, 
            string[] field, 
            string[] select,
            string q, 
            string job, 
            string and, 
            string or,
            int skip,
            int take)
        {
            bool isValid = true;
            ViewBag.JobValidationError = null;
            ViewBag.TargetCollectionValidationError = null;
            ViewBag.Collection = collection;
            ViewBag.Field = field;
            ViewBag.Q = q;
            ViewBag.Job = job;

            if (string.IsNullOrWhiteSpace(job))
            {
                ViewBag.JobValidationError = "Please select a job to execute.";
                isValid = false;
            }

            if (!isValid)
            {
                return View("Index");
            }

            _queue.Enqueue(new CrawlJob(
                _config.Get("data_dir"),
                SessionFactory,
                _queryParser,
                _model,
                _log,
                crawlid, 
                collection, 
                field, 
                q, 
                job, 
                and!=null, 
                or!=null,
                skip,
                take
            ));

            return RedirectToAction(job, "Status", new 
            { 
                crawlid,
                collection,
                field,
                select,
                q,
                and = (and != null ? "AND" : null),
                or = (or != null ? "OR" : null),
                skip,
                take
            });
        }
    }
}