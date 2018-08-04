using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}