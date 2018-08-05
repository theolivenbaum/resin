using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class AddController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Thankyou()
        {
            return View();
        }
    }
}