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
            if (string.IsNullOrWhiteSpace(url))
            {
                return View("Index");
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                return View("Index");
            }

            var collectionName = collectionId ?? "www";

            try
            {
                var uri = new Uri(url);
                var document = new Dictionary<string, object>();
                var parsed = GetWebString(uri);

                document["site"] = uri.Host;
                document["url"] = uri.ToString();
                document["body"] = parsed.body;
                document["title"] = parsed.title;
                document["created"] = DateTime.Now.ToBinary();

                var writers = _plugins.All<IWriter>(Request.ContentType).ToList();
                var reader = _plugins.Get<IReader>();
                var remover = _plugins.Get<IRemover>();

                if (writers == null || reader == null || remover == null)
                {
                    return StatusCode(415); // Media type not supported
                }

                remover.Remove(
                    new Query { CollectionId = collectionName.ToHash(), Term = new Term("url", uri.ToString()) },
                    reader);

                foreach (var writer in writers)
                {
                    await Task.Run(() =>
                    {
                        writer.Write(collectionName, new[] { document });
                    });
                }
                
                return Redirect("/add/thankyou");
            }
            catch (Exception ex)
            {
                // TODO: add logging framework
                System.IO.File.WriteAllText(string.Format("_{0}_{1}.log", DateTime.Now.ToBinary(), WebUtility.UrlEncode(url)), ex.ToString());

                return View("Error");
            }
        }

        private (string title, string body) GetHtml(Uri uri)
        {
            var htmlDoc = _htmlParser.Load(uri);
            return Parse(htmlDoc);
        }

        private (string title, string body) GetWebString(Uri uri)
        {
            var webRequest = WebRequest.Create(uri);
            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                var str = reader.ReadToEnd();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(str);
                return Parse(htmlDoc);
            }
        }

        private static (string title, string body) Parse(HtmlDocument htmlDocument)
        {
            var title = WebUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("//title").First().InnerText);
            var root = htmlDocument.DocumentNode.SelectNodes("//body").First();
            var txtNodes = root.Descendants().Where(x =>
                x.Name == "#text" &&
                (x.ParentNode.Name != "script") &&
                (!string.IsNullOrWhiteSpace(x.InnerText))
            ).ToList();

            var txt = txtNodes.Select(x => WebUtility.HtmlDecode(x.InnerText));
            var body = string.Join("\r\n", txt);

            return (title, body);
        }

        public ActionResult Thankyou()
        {
            return View();
        }
    }
}