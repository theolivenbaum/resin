﻿using Microsoft.AspNetCore.Mvc;
using Sir.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.HttpServer.Controllers
{
    public class CreateController : UIController
    {
        public CreateController(IConfigurationProvider config, Database database) : base(config, database)
        {
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

            var urlList = Request.Query["urls"].Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new Uri(s).ToString()).ToList();

            if (scope == "page")
            {
                urlList.Add(url.Replace("https://", "page://"));
            }
            else
            {
                urlList.Add(url.Replace("https://", "site://"));
            }

            var queryString = $"?urls={string.Join("&urls=", urlList.Select(s => Uri.EscapeDataString(s)))}";
            var returnUrl = $"{Request.Scheme}://{Request.Host}{queryString}";

            return Redirect(returnUrl);
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

        [HttpPost("/createindex")]
        public ActionResult CreateIndex(string[] urls, string agree)
        {
            if (agree != "yes")
            {
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = "It is required that you read and agree to the terms." });
            }

            if (urls.Length == 0 || urls[0] == null)
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = "URL list is empty." });

            //validate that all entries are parsable into Uris
            try
            {
                var uris = new List<Uri>();
                foreach (var url in urls)
                {
                    uris.Add(new Uri(url.Replace("page://", "https://").Replace("site://", "https://")));
                }
            }
            catch (Exception ex)
            {
                return View("/Views/Home/Index.cshtml", new CreateModel { ErrorMessage = $"URL list is not valid. {ex}" });
            }

            var queryId = Guid.NewGuid().ToString();
            var userDirectory = Path.Combine(Config.Get("user_dir"), queryId);

            if (!Directory.Exists(userDirectory))
            {
                Directory.CreateDirectory(userDirectory);
            }

            var collectionId = "url".ToHash();
            var documents = new List<Document>();

            foreach (var url in urls)
            {
                var uri = new Uri(url.Replace("page://", "https://").Replace("site://", "https://"));

                documents.Add(new Document(new Field[] 
                {
                    new Field(
                        "url",
                        uri.ToString(),
                        index: false,
                        store: true),
                    new Field(
                        "host",
                        uri.Host,
                        index: false,
                        store: true),
                    new Field(
                        "last_crawl_date",
                        DateTime.MinValue,
                        index: false,
                        store: true),
                    new Field(
                        "scope", 
                        url.StartsWith("page://") ? "page" : "site", 
                        index:false, 
                        store:true)
                }));
            }

            Database.Store(
                userDirectory,
                collectionId,
                documents);

            return RedirectToAction("Index", "Search", new { queryId });
        }
    }

    public class CreateModel
    {
        public string ErrorMessage { get; set; }
    }
}
