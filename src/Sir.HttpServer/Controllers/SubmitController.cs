using System;
using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class SubmitController : UIController
    {
        private readonly HtmlWeb _htmlParser;
        private readonly CrawlQueue _crawlQueue;
        private readonly ILogger<SubmitController> _logger;

        public SubmitController(
            PluginsCollection plugins, 
            CrawlQueue crawlQueue, 
            IConfigurationProvider config, 
            ISessionFactory sessionFactory,
            ILogger<SubmitController> logger) : base(config, sessionFactory)
        {
            _htmlParser = new HtmlWeb();
            _crawlQueue = crawlQueue;
            _logger = logger;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet("/submit/page")]
        public IActionResult Page(string url, string collection)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return View("Index");
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                return View("Index");
            }

            var collectionName = collection ?? Config.Get("default_collection");

            try
            {
                var uri = new Uri(url);
                _crawlQueue.Enqueue(collectionName, uri);

                var q = Uri.EscapeDataString(_crawlQueue.GetTitle(uri));
             
                return Redirect($"/submitpage/thankyou?url={url}&q={q}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"{WebUtility.UrlEncode(url)} {ex}");

                return View("Error");
            }
        }

        [HttpGet("/submitpage/thankyou")]
        public ActionResult Thankyou()
        {
            return View();
        }
    }
}