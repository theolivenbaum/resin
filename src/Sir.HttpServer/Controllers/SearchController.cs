using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class SearchController : Controller
    {
        public ActionResult Index(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return View();
            }

            if (!q.Contains(":"))
            {
                q = string.Format("title:{0}\nbody:{0}", q);
            }

            return RedirectToAction("Get", "IO", new { collectionId = "mycol", query = q });
        }
    }
}