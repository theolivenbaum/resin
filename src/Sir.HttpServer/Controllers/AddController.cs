using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace Sir.HttpServer.Controllers
{
    public class AddController : Controller
    {
        private PluginsCollection _plugins;
        private readonly HtmlWeb _htmlParser;

        public AddController(PluginsCollection plugins)
        {
            _plugins = plugins;
            _htmlParser = new HtmlWeb();
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet("/add/page")]
        public async Task<IActionResult> Page(string url, string collectionId)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var collectionName = collectionId ?? "www";
            var uri = new Uri(url);
            var document = new Dictionary<string, object>();
            var htmlDoc = _htmlParser.Load(uri);
            var title = htmlDoc.DocumentNode.SelectNodes("//title").First().InnerText;
            var root = htmlDoc.DocumentNode.SelectNodes("//body").First();
            var txtNodes = root.Descendants().Where(x => 
                x.Name == "#text" && 
                (x.ParentNode.Name != "script") && 
                (!string.IsNullOrWhiteSpace(x.InnerText))
            ).ToList();

            var txt = txtNodes.Select(x => x.InnerText);
            var body = string.Join("\n", txt);

            System.IO.File.WriteAllText(DateTime.Now.Ticks + "_" + uri.Host + ".txt", body);

            if (string.IsNullOrWhiteSpace(title))
            {
                title = string.Join(string.Empty, txt.Take(3));
            }

            document["site"] = uri.Host;
            document["url"] = uri.ToString();
            document["body"] = body;
            document["title"] = title;

            var writers = _plugins.All<IWriter>(Request.ContentType).ToList();

            if (writers == null || writers.Count == 0)
            {
                return StatusCode(415); // Media type not supported
            }

            foreach (var writer in writers)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        writer.Write(collectionName, new[] { document });
                    });
                }
                catch (Exception ew)
                {
                    throw ew;
                }
            }

            return Redirect("/add/thankyou");

            //return StatusCode(201); // Created
        }

        private static string GetWebString(Uri uri)
        {
            var webRequest = WebRequest.Create(uri);
            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                return reader.ReadToEnd();
            }
        }

        public ActionResult Thankyou()
        {
            return View();
        }
    }
}