using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.HttpServer.Controllers
{
    public class OptionsController : UIController
    {
        public OptionsController(IConfigurationProvider config, Database database) : base(config, database)
        {
        }

        [HttpGet("/options")]
        public ActionResult Index(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            return View();
        }

        [HttpGet("/options/urls")]
        public ActionResult Urls(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            var urlList = new List<string>();

            foreach (var document in Database.Select(userDirectory, "url".ToHash(), new HashSet<string> { "url", "scope" }))
            {
                string url = null;
                string scope = null;

                foreach(var field in document.Fields)
                {
                    if (field.Name == "url")
                        url = (string)field.Value;
                    else if (field.Name == "scope")
                        scope = (string)field.Value;
                }

                if (scope == "page")
                    urlList.Add(url.Replace("https://", "page://"));
                else
                    urlList.Add(url.Replace("https://", "site://"));
            }

            ViewData["urls"] = urlList;

            return View();
        }

        [HttpPost("/options/urls/save")]
        public ActionResult SaveUrls(string[] urls, string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            if (urls.Length == 0 || urls[0] == null)
            {
                var returnUri = new Uri($"/options/urls?urls=&queryId={queryId}&errorMessage=URL list is empty.", UriKind.Relative);

                return Redirect(returnUri.ToString());
            }

            var uris = new List<(Uri uri, string scope)>();

            //validate that all entries are parsable into Uris
            try
            {
                foreach (var url in urls)
                {
                    uris.Add((new Uri(url.Replace("page://", "https://").Replace("site://", "https://")), url.StartsWith("page://") ? "page" : "site"));
                }
            }
            catch (Exception ex)
            {
                var returnUrl = $"/options/urls?urls={string.Join("&urls=", urls.Select(s => Uri.EscapeDataString(s)))}&errorMessage=URL list is not valid. {ex}";
                var returnUri = new Uri(returnUrl, UriKind.Relative);

                return Redirect(returnUri.ToString());
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            try
            {
                if (!Directory.Exists(userDirectory))
                {
                    return new ConflictResult();
                }

                var urlCollectionId = "url".ToHash();

                Database.Truncate(userDirectory, urlCollectionId);

                var documents = new List<Document>();

                foreach (var uri in uris)
                {
                    documents.Add(new Document(new Field[]
                    {
                        new Field("url", uri.uri.ToString()),
                        new Field("host", uri.uri.Host),
                        new Field("scope", uri.scope),
                        new Field("verified", false)
                    }));
                }

                Database.Store(
                    userDirectory,
                    urlCollectionId,
                    documents);

                return RedirectToAction("Urls", "Options", new { queryId });
            }
            catch
            {
                return new ConflictResult();
            }
        }

        [HttpGet("/options/updatefrequency")]
        public ActionResult UpdateFrequency(string queryId)
        {
            if (queryId is null)
            {
                return NotFound();
            }

            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
                return NotFound();

            return View();
        }
    }
}
