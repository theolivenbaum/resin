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
            try
            {
                var uri = new Uri(url);
            }
            catch (Exception ex)
            {
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = ex.Message });
            }

            var urlList = Request.Query["urls"].ToList();

            if (urlList.Count == 0)
            {
                urlList.Add("https://en.wikipedia.org");
            }

            urlList.Add(url);

            var queryString = $"?urls={string.Join("&urls=", urlList.Select(s=>Uri.EscapeDataString(s)))}";
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
        public string ErrorMessage { get; set; }
    }
}
