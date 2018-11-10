using System;
using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class AddController : UIController
    {
        private PluginsCollection _plugins;
        private readonly HtmlWeb _htmlParser;
        private readonly CrawlQueue _crawlQueue;

        public AddController(PluginsCollection plugins, CrawlQueue crawlQueue, IConfigurationService config) : base(config)
        {
            _plugins = plugins;
            _htmlParser = new HtmlWeb();
            _crawlQueue = crawlQueue;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet("/add/page")]
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
                
                return Redirect("/add/thankyou");
            }
            catch (Exception ex)
            {
                // TODO: add logging framework
                System.IO.File.WriteAllText(string.Format("_{0}_{1}.log", DateTime.Now.ToBinary(), WebUtility.UrlEncode(url)), ex.ToString());

                return View("Error");
            }
        }

        public ActionResult Thankyou()
        {
            return View();
        }
    }
}