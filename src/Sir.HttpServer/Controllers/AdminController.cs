using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sir.CommonCrawl;
using Sir.Search;
using System.Net;

namespace Sir.HttpServer.Controllers
{
    public class AdminController : UIController
    {
        private readonly IConfigurationProvider _config;
        private readonly ILogger<AdminController> _logger;
        private readonly IStringModel _model;

        public AdminController(
            IConfigurationProvider config, 
            SessionFactory sessionFactory,
            ILogger<AdminController> logger,
            IStringModel model) 
            : base(config, sessionFactory)
        {
            _config = config;
            _logger = logger;
            _model = model;
        }

        [Route("/admin")]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult DownloadAndIndexWat(
            string accessToken,
            string commonCrawlId,
            string collectionName,
            int skip,
            int take
            )
        {
            if (!IsValidToken(accessToken))
            {
                return StatusCode((int)HttpStatusCode.MethodNotAllowed);
            }

            CCHelper.DownloadAndIndexWat(
                commonCrawlId, 
                _config.Get("data_dir"), 
                collectionName, 
                skip, 
                take,
                _model,
                _logger);

            return View();
        }

        private bool IsValidToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            return _config.Get("admin_password").Equals(accessToken);
        }
    }
}