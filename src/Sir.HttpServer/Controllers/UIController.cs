using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sir.HttpServer.Controllers
{
    public abstract class UIController : Controller
    {
        private readonly IConfigurationProvider _config;
        private readonly ISessionFactory _sessionFactory;
        private IConfigurationProvider config;

        protected IConfigurationProvider Config { get { return _config; } }

        public UIController(IConfigurationProvider config, ISessionFactory sessionFactory)
        {
            _config = config;
            _sessionFactory = sessionFactory;
        }

        protected UIController(IConfigurationProvider config)
        {
            this.config = config;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            ViewBag.DefaultCollection = _config.Get("default_collection");

            ViewData["doc_count"] = context.HttpContext.Request.Query.ContainsKey("collection") ? 
                _sessionFactory.GetDocCount(context.HttpContext.Request.Query["collection"].ToString()) :
                _sessionFactory.GetDocCount(ViewBag.DefaultCollection);

            ViewBag.Collection = context.HttpContext.Request.Query.ContainsKey("collection") ?
                context.HttpContext.Request.Query["collection"].ToString() :
                ViewBag.DefaultCollection;

            base.OnActionExecuted(context);
        }
    }
}