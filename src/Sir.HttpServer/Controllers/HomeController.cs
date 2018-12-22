using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        private readonly IConfigurationProvider _config;

        public HomeController(IConfigurationProvider config) : base(config)
        {
            _config = config;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}