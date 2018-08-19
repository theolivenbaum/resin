using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sir.HttpServer.Controllers
{
    public abstract class UIController : Controller
    {
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            long? docCount = null;
            string fileName = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "6604389855880847730.docs");
            if (System.IO.File.Exists(fileName))
            {
                docCount = new FileInfo(fileName).Length/75;
            }
            ViewData["doc_count"] = docCount;
            base.OnActionExecuted(context);
        }
    }
}