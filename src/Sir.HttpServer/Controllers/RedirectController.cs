using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class RedirectController : UIController
    {
        public RedirectController(IConfigurationProvider config) : base(config)
        {
        }

        [HttpGet("label-and-redirect")]
        public ActionResult Index(string url)
        {
            return Redirect("https://" + System.Uri.UnescapeDataString(url));
        }
    }
}