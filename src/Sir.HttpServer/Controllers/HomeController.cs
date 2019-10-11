using Microsoft.AspNetCore.Mvc;
using Sir.Store;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        private readonly IConfigurationProvider _config;

        public HomeController(IConfigurationProvider config, ISessionFactory sessionFactory) : base(config, sessionFactory)
        {
            _config = config;
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}