using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.HttpServer.Controllers
{
    public class CreateController : UIController
    {
        public CreateController(IConfigurationProvider config, SessionFactory sessionFactory) : base(config, sessionFactory)
        {
        }

        [HttpGet("/addurl")]
        public ActionResult AddUrl(string url)
        {
            var urlList = Request.Query["urls"].ToList();
            urlList.Add(url);
            var queryString = $"?urls={string.Join("&urls=", urlList)}";
            var returnUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{queryString}";
            return Redirect(returnUrl);
        }

        [HttpPost("/create")]
        public ActionResult Index([FromForm]CreateModel model)
        {
            return View();
        }
    }

    public class CreateModel
    {
        [FromForm(Name = "sitelist")]
        public IList<string> SiteList { get; set; }
    }
}
