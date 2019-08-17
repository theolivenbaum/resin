using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using Sir.Core;
using Sir.Store;

namespace Sir.HttpServer.Features
{
    public class CrawlQueue : IDisposable, ILogger
    {
        private readonly ProducerConsumerQueue<(string collection, Uri uri)> _queue;
        private readonly SessionFactory _sessionFactory;
        private readonly IStringModel _model;

        public (Uri uri, string title) LastProcessed { get; private set; }

        public CrawlQueue(SessionFactory sessionFactory, IStringModel model)
        {
            _queue = new ProducerConsumerQueue<(string,Uri)>(1, Submit);
            _sessionFactory = sessionFactory;
            _model = model;
        }

        public void Enqueue(string collection, Uri uri)
        {
            _queue.Enqueue((collection, uri));
        }

        public string GetTitle(Uri uri)
        {
            try
            {
                var url = uri.ToString().Replace(uri.Scheme + "://", string.Empty);
                var str = GetWebString(uri);

                if (str == null)
                {
                    return string.Empty;
                }

                var html = new HtmlDocument();

                html.LoadHtml(str);

                var doc = Parse(html, uri);

                if (doc.title == null)
                {
                    return string.Empty;
                }

                return doc.title;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void Submit((string collection, Uri uri) item)
        {
            try
            {
                var url = item.uri.ToString().Replace(item.uri.Scheme + "://", string.Empty);
                var robotTxt = GetWebString(new Uri(string.Format("{0}://{1}/robots.txt", item.uri.Scheme, item.uri.Host)));
                var allowed = true;

                if (robotTxt != null)
                {
                    var robotRules = GetForbiddenUrls(robotTxt);
                    var uriStr = item.uri.ToString();

                    foreach (var rule in robotRules)
                    {
                        if (uriStr.Contains(rule))
                        {
                            allowed = false;
                            break;
                        }
                    }
                }

                if (!allowed)
                {
                    this.Log("url forbidden by robot.txt: {0}", item.uri);

                    return;
                }

                var str = GetWebString(item.uri);

                if (str == null)
                {
                    return;
                }

                var html = new HtmlDocument();

                html.LoadHtml(str);

                var doc = Parse(html, item.uri);

                if (doc.title == null)
                {
                    this.Log(string.Format("error processing {0} (no title)", item.uri));
                    return;
                }

                var document = new Dictionary<string, object>();

                document["_url"] = url;
                document["title"] = doc.title;
                document["body"] = doc.body;

                ExecuteWrite(item.collection, document);

                LastProcessed = (item.uri, (string)document["title"]);
            }
            catch (Exception ex)
            {
                this.Log(string.Format("error processing {0} {1}", item.uri, ex));
            }
        }

        private IDictionary<string, object> GetDocument(string collectionName, string url, string title)
        {
            using (var readSession = _sessionFactory.CreateReadSession(collectionName.ToHash()))
            {
                const string key = "_url";
                var keyId = _sessionFactory.GetKeyId(collectionName.ToHash(), key.ToHash());
                var urlQuery = new Query(collectionName.ToHash(), keyId, _model.Tokenize(url));
                urlQuery.And = true;

                var result = readSession.Read(new Query[1] { urlQuery }, 0, 1).Docs.ToList();
            
                return result.Count == 0 
                    ? null : (float)result[0]["___score"] >= _model.IdenticalAngle 
                    ? result[0] : null;
            }
        }

        public void ExecuteWrite(string collectionName, IDictionary<string, object> doc)
        {
            _sessionFactory.Write(new Job(collectionName.ToHash(), new[] { doc }, _model));
        }

        private string GetWebString(Uri uri)
        {
            var urlStr = uri.ToString();

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.ReadWriteTimeout = 10 * 1000;
                req.Headers.Add("User-Agent", "dygg-robot/didyougogo.com");
                req.Headers.Add("Accept", "text/html");

                using (var response = (HttpWebResponse)req.GetResponse())
                using (var content = response.GetResponseStream())
                using (var reader = new StreamReader(content))
                {
                    if (!response.GetResponseHeader("Content-Type").Contains("text/html"))
                    {
                        return null;
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        this.Log(string.Format("bad request: {0} response: {1}", uri, response.StatusCode));

                        return null;
                    }

                    this.Log(string.Format("requested: {0}", uri));

                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                this.Log(string.Format("request failed: {0} {1}", uri, ex));

                return null;
            }
        }

        private static HashSet<string> GetForbiddenUrls(string robotTxt)
        {
            var result = new HashSet<string>();

            foreach (var line in robotTxt.Split('\r', '\n'))
            {
                var parts = line.ToLower().Split(':');

                if (parts[0].Trim() == "disallow")
                {
                    var rule = parts[1].Trim(' ', '?').Replace("/*", string.Empty);

                    if (rule!="/")
                        result.Add(rule);
                }
            }

            return result;
        }

        private (string title, string body) Parse(HtmlDocument htmlDocument, Uri owner)
        {
            var titleNodes = htmlDocument.DocumentNode.SelectNodes("//title");

            if (titleNodes == null) return (null, null);

            var titleNode = titleNodes.FirstOrDefault();

            if (titleNode == null) return (null, null);

            var title = WebUtility.HtmlDecode(titleNode.InnerText);
            var root = htmlDocument.DocumentNode.SelectNodes("//body").First();
            var txtNodes = root.Descendants().Where(x =>
                x.Name == "#text" &&
                (x.ParentNode.Name != "script") &&
                (!string.IsNullOrWhiteSpace(x.InnerText))
            ).ToList();

            var ownerUrl = owner.Host;
            var txt = txtNodes.Select(x => WebUtility.HtmlDecode(x.InnerText));
            var body = string.Join("\r\n", txt);

            return (title, body);
        }

        public void Dispose()
        {
            _queue.Dispose();
        }
    }
}
