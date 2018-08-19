using Microsoft.AspNetCore.Mvc;
using Sir.HttpServer.Features;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        private readonly CrawlQueue _crawlQueue;

        public HomeController(CrawlQueue crawlQueue)
        {
            _crawlQueue = crawlQueue;
        }

        public ActionResult Index()
        {
            ViewData["last_processed_url"] = _crawlQueue.LastProcessed.uri;
            ViewData["last_processed_title"] = _crawlQueue.LastProcessed.title;

            return View();
        }
    }
}