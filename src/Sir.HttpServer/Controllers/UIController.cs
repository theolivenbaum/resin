using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sir.Search;

namespace Sir.HttpServer.Controllers
{
    public abstract class UIController : Controller
    {
        private readonly StreamFactory _sessionFactory;
        private IConfigurationProvider config;

        protected IConfigurationProvider Config { get; }
        protected StreamFactory StreamFactory => _sessionFactory;

        public UIController(IConfigurationProvider config, StreamFactory sessionFactory)
        {
            Config = config;
            _sessionFactory = sessionFactory;
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