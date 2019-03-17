using System;
using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class SubmitController : UIController, ILogger
    {
        private readonly HtmlWeb _htmlParser;
        private readonly CrawlQueue _crawlQueue;

        public SubmitController(PluginsCollection plugins, CrawlQueue crawlQueue, IConfigurationProvider config) : base(config)
        {
            _htmlParser = new HtmlWeb();
            _crawlQueue = crawlQueue;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet("/submit/page")]
        public IActionResult Page(string url, string collectionId)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return View("Index");
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                return View("Index");
            }

            var collectionName = collectionId ?? "www";

            try
            {
                _crawlQueue.Enqueue(new Uri(url));
             
                return Redirect("/submitpage/thankyou?url=" + url);
            }
            catch (Exception ex)
            {
                this.Log("{0} {1}", WebUtility.UrlEncode(url), ex);

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