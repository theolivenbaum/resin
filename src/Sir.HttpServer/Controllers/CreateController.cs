using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.IO;
using System.Linq;

namespace Sir.HttpServer.Controllers
{
    public class CreateController : UIController
    {
        public CreateController(IConfigurationProvider config, SessionFactory sessionFactory) : base(config, sessionFactory)
        {
        }

        [HttpGet("/deleteurl")]
        public ActionResult DeleteUrl(string url)
        {
            if (url is null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var urlList = Request.Query["urls"].ToList();

            urlList.Remove(url);

            var queryString = $"?urls={string.Join("&urls=", urlList.Select(s => Uri.EscapeDataString(s)))}";

            var returnUrl = $"{Request.Scheme}://{Request.Host}{queryString}";

            return Redirect(returnUrl);
        }

        [HttpGet("/createindex")]
        public ActionResult CreateIndex(string[] urls, string agree)
        {
            if (agree != "yes")
            {
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = "It is required that you agree to the terms." });
            }

            if (urls.Length == 0 || urls[0] == null)
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = "URL list is empty." });

            var queryId = Guid.NewGuid().ToString();
            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
            {
                Directory.CreateDirectory(userDirectory);
            }

            var model = new BagOfCharsModel();
            var tree = new VectorNode();
            var collectionId = "query".ToHash();

            SessionFactory.Write(
                userDirectory,
                collectionId,
                urls.Select(url => new Document(new Field[] { new Field("url", url, index: true, store: true) })),
                model);

            for (int i = 0;i < urls.Length;i++)
            {
                foreach (var vector in model.Tokenize(urls[i]))
                {
                    tree.MergeOrAddConcurrent(new VectorNode(vector: vector, docId: i), model);
                }
            }

            return RedirectToAction("Index", "Search", new { queryId, field = new string[] { "title", "text" } });
        }

        [HttpGet("/addurl")]
        public ActionResult AddUrl(string url, string scope)
        {
            Uri uri;

            try
            {
                uri = new Uri(url);

                if (uri.Scheme != "https")
                    throw new Exception("Scheme was http. Scheme must be https.");
            }
            catch (Exception ex)
            {
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = ex.Message });
            }

            var urlList = Request.Query["urls"].ToList();

            if (urlList.Count == 0)
            {
                urlList.Add("site://en.wikipedia.org");
            }

            if (scope == "page")
            {
                urlList.Add(url.Replace("https://", "page://"));
            }
            else
            {
                urlList.Add(url.Replace("https://", "site://"));
            }

            var queryString = $"?urls={string.Join("&urls=", urlList.Select(s=>Uri.EscapeDataString(s)))}";
            var returnUrl = $"{Request.Scheme}://{Request.Host}{queryString}";

            return Redirect(returnUrl);
        }
    }

    public class CreateModel
    {
        public string ErrorMessage { get; set; }
    }
}
