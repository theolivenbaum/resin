using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Sir.Core;
using Sir.Store;

namespace Sir.HttpServer.Features
{
    public class CrawlQueue : IDisposable, ILogger
    {
        private readonly ProducerConsumerQueue<Uri> _queue;
        private readonly SessionFactory _sessionFactory;

        public (Uri uri, string title) LastProcessed { get; private set; }

        public CrawlQueue(SessionFactory sessionFactory)
        {
            _queue = new ProducerConsumerQueue<Uri>(1, callback: Submit);
            _sessionFactory = sessionFactory;
        }

        public void Enqueue(Uri uri)
        {
            _queue.Enqueue(uri);
        }

        private async Task Submit(Uri uri)
        {
            try
            {
                var url = uri.ToString().Replace(uri.Scheme + "://", string.Empty);
                var robotTxt = GetWebString(new Uri(string.Format("{0}://{1}/robots.txt", uri.Scheme, uri.Host)));
                var allowed = true;

                if (robotTxt != null)
                {
                    var robotRules = GetForbiddenUrls(robotTxt);
                    var uriStr = uri.ToString();

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
                    this.Log("url forbidden by robot.txt: {0}", uri);

                    return;
                }

                var str = GetWebString(uri);

                if (str == null)
                {
                    return;
                }

                var html = new HtmlDocument();

                html.LoadHtml(str);

                var doc = Parse(html, uri);

                if (doc.title == null)
                {
                    this.Log(string.Format("error processing {0} (no title)", uri));
                    return;
                }

                var document = new Dictionary<string, object>();
                var existing = GetDocument("www", url, doc.title);

                if (existing!= null)
                {
                    document["_original"] = existing["__docid"];
                }

                document["_site"] = uri.Host;
                document["_url"] = url;
                document["title"] = doc.title;
                document["body"] = doc.body;
                document["_created"] = DateTime.Now.ToBinary();

                await ExecuteWrite("www", document);
                
                LastProcessed = (uri, (string)document["title"]);
            }
            catch (Exception ex)
            {
                this.Log(string.Format("error processing {0} {1}", uri, ex));
            }
        }

        private IDictionary GetDocument(string collectionName, string url, string title)
        {
            using (var readSession = _sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
            {
                var urlQuery = new Query(collectionName.ToHash(), new Term("_url", new VectorNode(url)));
                urlQuery.And = true;
                urlQuery.Take = 1;

                var result = readSession.Read(urlQuery);
            
                return result.Total == 0 
                    ? null : (float)result.Docs[0]["__score"] >= VectorNode.TermIdenticalAngle 
                    ? result.Docs[0] : null;
            }
        }

        private async Task<IList<long>> ExecuteWrite(string collectionName, IDictionary document)
        {
            var time = Stopwatch.StartNew();
            IList<long> docIds;

            using (var write = _sessionFactory.CreateWriteSession(collectionName, collectionName.ToHash()))
            {
                docIds = await write.Write(new[] { document });
            }

            if (docIds.Count > 0)
            {
                var skip = (int)docIds[0] - 1;
                var take = docIds.Count;

                using (var docs = _sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var index = _sessionFactory.CreateIndexSession(collectionName, collectionName.ToHash()))
                {
                    foreach (var doc in docs.ReadDocs(skip, take))
                    {
                        index.EmbedTerms(doc);
                    }
                }
            }

            this.Log("executed {0} write+index job in {1}", collectionName, time.Elapsed);

            return docIds;
        }

        private string GetWebString(Uri uri)
        {
            var urlStr = uri.ToString();

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.ReadWriteTimeout = 10 * 1000;
                req.Headers.Add("User-Agent", "gogorobot/didyougogo.com");
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
