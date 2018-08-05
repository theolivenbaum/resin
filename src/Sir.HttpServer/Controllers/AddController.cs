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

        [HttpPost]
        public ActionResult Post(string url)
        {
            var str = GetWebString(url.StartsWith("https://") ? url : string.Format("https://{0}", url));
            var decoded = WebUtility.HtmlDecode(str);
            var parser = new YesNoParser('>', '<', new string[] { "script" });
            var parsed = parser.Parse(decoded);

            return RedirectToAction("¨Post", "IO", new { collectionId = "mycol" });
        }

        private static string GetWebString(string url)
        {
            var webRequest = WebRequest.Create(url);
            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                return reader.ReadToEnd();
            }
        }
    }
}