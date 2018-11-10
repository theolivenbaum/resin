using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        private readonly IConfigurationService _config;

        public HomeController(IConfigurationService config) : base(config)
        {
            _config = config;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}