using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : UIController
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}