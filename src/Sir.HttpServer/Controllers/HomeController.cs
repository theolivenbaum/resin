using Microsoft.AspNetCore.Mvc;
using Sir.Search;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        private readonly IConfigurationProvider _config;

        public HomeController(IConfigurationProvider config, Database sessionFactory) 
            : base(config, sessionFactory)
        {
            _config = config;
        }

        public ActionResult Index()
        {
            return View();
        }

        [Route("/terms")]
        public ActionResult Terms()
        {
            return View();
        }

        [Route("/about")]
        public ActionResult About()
        {
            return View();
        }

        [Route("/contact")]
        public ActionResult Contact()
        {
            return View();
        }

        [Route("/buymecoffee")]
        public ActionResult BuyMeCoffee()
        {
            return View();
        }
    }
}