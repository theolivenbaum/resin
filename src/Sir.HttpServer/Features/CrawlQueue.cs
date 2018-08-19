using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using Sir.Core;

namespace Sir.HttpServer.Features
{
    public class CrawlQueue
    {
        private readonly ProducerConsumerQueue<Uri> _queue;
        private readonly PluginsCollection _plugins;
        private HashSet<string> _history;
        private StreamWriter _log;

        public CrawlQueue(PluginsCollection plugins)
        {
            _queue = new ProducerConsumerQueue<Uri>(Submit, 1000);
            _plugins = plugins;
            _history = new HashSet<string>();
            _log = new StreamWriter(
                File.Open("crawlqueue.log", FileMode.Append, FileAccess.Write, FileShare.Read));
        }

        public void Enqueue(Uri uri)
        {
            _queue.Enqueue(uri);
        }

        private void Submit(Uri uri)
        {
            if (!_history.Add(uri.Host))
            {
                return;
            }

            try
            {
                var req = WebRequest.Create(uri);
                req.Headers.Add("User-Agent", "gogorobot,didyougogo.com");
                req.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;");

                var document = new Dictionary<string, object>();

                using (var response = req.GetResponse())
                using (var content = response.GetResponseStream())
                using (var reader = new StreamReader(content))
                {
                    _log.Log(string.Format("requested {0}", uri));

                    var str = reader.ReadToEnd();
                    var htmlDoc = new HtmlDocument();

                    htmlDoc.LoadHtml(str);

                    var doc = Parse(htmlDoc, uri);

                    document["_site"] = uri.Host;
                    document["_url"] = uri.ToString().Replace(uri.Scheme + "://", string.Empty);
                    document["body"] = doc.body;
                    document["title"] = doc.title;
                    document["_created"] = DateTime.Now.ToBinary();

                    var writers = _plugins.All<IWriter>("*").ToList();

                    foreach (var writer in writers)
                    {
                        writer.Write("www", new[] { document });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("error processing {0} {1}", uri, ex));
            }
        }

        private (string title, string body) Parse(HtmlDocument htmlDocument, Uri owner)
        {
            var title = WebUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("//title").First().InnerText);
            var root = htmlDocument.DocumentNode.SelectNodes("//body").First();
            var txtNodes = root.Descendants().Where(x =>
                x.Name == "#text" &&
                (x.ParentNode.Name != "script") &&
                (!string.IsNullOrWhiteSpace(x.InnerText))
            ).ToList();

            var ownerUrl = owner.Host;
            var txt = txtNodes.Select(x => WebUtility.HtmlDecode(x.InnerText));
            var body = string.Join("\r\n", txt);
            var links = htmlDocument.DocumentNode.SelectNodes("//a[@href]")
                .Select(x => x.Attributes["href"] == null ? null : x.Attributes["href"].Value)
                .Where(x => (x != null && x.StartsWith("https://") && (!x.Contains(ownerUrl))))
                .ToList();

            foreach (var url in links)
            {
                _queue.Enqueue(new Uri(url));
            }

            return (title, body);
        }
    }

    public static class LoggingExtensions
    {
        public static void Log(this StreamWriter writer, string message)
        {
            writer.WriteLine(string.Format("{0} {1}", DateTime.Now, message));
            writer.Flush();
        }
    }
}
