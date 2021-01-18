using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sir.Search;
using System.Collections.Generic;
using System.IO;

namespace Sir.HttpServer.Controllers
{
    public abstract class UIController : Controller
    {
        private IConfigurationProvider config;

        protected IConfigurationProvider Config { get; }
        protected Database Database { get; }

        public UIController(IConfigurationProvider config, Database database)
        {
            Config = config;
            Database = database;
        }

        protected UIController(IConfigurationProvider config)
        {
            this.config = config;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            ViewBag.DefaultCollection = Config.GetMany("default_collection");
            ViewBag.DefaultFields = Config.GetMany("default_fields");
            ViewBag.Collection = context.HttpContext.Request.Query.ContainsKey("collection") ?
                context.HttpContext.Request.Query["collection"].ToArray() :
                ViewBag.DefaultCollection;
           
            base.OnActionExecuted(context);
        }
    }
}