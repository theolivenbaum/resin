using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sir.HttpServer.Controllers
{
    public abstract class UIController : Controller
    {
        private readonly IConfigurationProvider _config;

        public UIController(IConfigurationProvider config)
        {
            _config = config;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            long? docCount = null;
            string fileName = Path.Combine(_config.Get("data_dir"), "6604389855880847730.docs");
            if (System.IO.File.Exists(fileName))
            {
                docCount = new FileInfo(fileName).Length/111;
            }
            ViewData["doc_count"] = docCount;
            base.OnActionExecuted(context);
        }
    }
}