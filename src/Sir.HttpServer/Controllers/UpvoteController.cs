using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Sir.HttpServer.Controllers
{
    public class UpvoteController : UIController
    {
        public UpvoteController(IConfigurationProvider config) : base(config)
        {
        }

        [HttpGet("upvote")]
        public ActionResult Index(string collection, string q, string url, string OR, string AND, string[] fields)
        {
            ViewData["url"] = url;
            ViewData["q"] = q;

            return Redirect(Request.Headers["referer"]);
        }
    }
}