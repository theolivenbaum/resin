using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using System.IO;

namespace Sir.HttpServer.Controllers
{
    public class OptionsController : UIController
    {
        public OptionsController(IConfigurationProvider config, Database database) : base(config, database)
        {
        }

        [HttpGet("/options")]
        public ActionResult Index(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            return View();
        }

        [HttpGet("/options/urls")]
        public ActionResult Urls(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            return View();
        }

        [HttpGet("/options/update")]
        public ActionResult Update(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            return View();
        }
    }
}
