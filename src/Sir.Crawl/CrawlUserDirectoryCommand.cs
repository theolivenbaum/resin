using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sir.Crawl
{
    public class CrawlUserDirectoryCommand : ICommand
    {
        private readonly HashSet<string> _select = new HashSet<string> { "url", "scope", "last_crawl_date" };
        private readonly HashSet<string> _history = new HashSet<string>();
        private readonly IModel<string> _model = new BagOfCharsModel();

        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var userDirectory = args["userDirectory"];
            var urlCollectionId = "url".ToHash();
            var htmlClient = new HtmlWeb();

            htmlClient.UserAgent = "Crawlcrawler (+https://crawlcrawler.com)";

#if DEBUG
            htmlClient.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36";
#endif

            using (var database = new Database(logger))
            {
                foreach (var directory in Directory.EnumerateDirectories(userDirectory))
                {
                    var lastCrawlDateKeyId = database.GetKeyId(directory, urlCollectionId, urlCollectionId);

                    foreach (var url in Urls(directory, urlCollectionId, database))
                    {
                        DateTime lastCrawlDate = DateTime.MinValue;
                        Uri uri = null;
                        string scope = null;

                        foreach (var field in url.Fields)
                        {
                            if (field.Name == "url")
                                uri = new Uri((string)field.Value);
                            else if (field.Name == "scope")
                                scope = (string)field.Value;
                            else
                                lastCrawlDate = (DateTime)field.Value;
                        }

                        if (lastCrawlDate > DateTime.MinValue)
                            continue;

                        var collectionId = uri.Host.ToHash();
                        var siteWide = scope == "site";

                        try
                        {
                            var time = Stopwatch.StartNew();
                            var timeOfCrawl = DateTime.Now;
                            var documents = DoCrawl(uri, htmlClient, siteWide, logger).ToList();

                            logger.LogInformation($"crawling {documents.Count} resources from {uri.Host} took {time.Elapsed}.");

                            database.Write(dataDirectory, collectionId, documents, _model);
                            database.Update(directory, urlCollectionId, url.Id, lastCrawlDateKeyId, timeOfCrawl);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Crawl error");
                        }
                    }
                }
            }
        }

        private IEnumerable<Document> DoCrawl(Uri uri, HtmlWeb htmlClient, bool siteWide, ILogger logger)
        {
            if (!_history.Add(uri.ToString()))
            {
                yield break;
            }

            var doc = htmlClient.Load(uri);
            var title = doc.DocumentNode.Descendants("title").FirstOrDefault().InnerText;
            var sb = new StringBuilder();

            logger.LogInformation($"crawled {uri}");

            foreach (var node in doc.DocumentNode.DescendantsAndSelf())
            {
                if (!node.HasChildNodes && node.ParentNode.Name != "script" && node.ParentNode.Name != "style")
                {
                    string innerText = node.InnerText;

                    if (!string.IsNullOrEmpty(innerText))
                        sb.AppendLine(innerText.Trim());
                }
            }

            var text = sb.ToString();

            yield return new Document(new Field[]
            {
                new Field("title", title),
                new Field("text", text.ToString()),
                new Field("url", uri.ToString())
            });

            if (siteWide)
            {
                var root = $"{uri.Scheme}://{uri.Host}{uri.PathAndQuery}";

                foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    var href = link.Attributes["href"].Value;
                    Uri linkUri = null;

                    try
                    {
                        if (href.StartsWith('/'))
                        {
                            linkUri = new Uri($"{root}{href.Substring(1)}");
                        }
                        else if (href.StartsWith("http"))
                        {
                            linkUri = new Uri(href);
                        }
                        else if (href.Contains('/'))
                        {
                            linkUri = new Uri($"{root}{uri.PathAndQuery}{href}");
                        }
                    }
                    catch { }

                    if (linkUri != null)
                    {
                        if (linkUri.Host == uri.Host)
                        {
                            if (!_history.Add(linkUri.ToString()))
                            {
                                continue;
                            }

                            foreach (var document in DoCrawl(linkUri, htmlClient, siteWide: false, logger))
                            {
                                yield return document;

                                Thread.Sleep(1000);
                            }
                        }
                    }
                }
            }
        }

        private IEnumerable<Document> Urls(string directory, ulong collectionId, Database streamFactory)
        {
            using (var reader = new DocumentStreamSession(directory, streamFactory))
            {
                foreach (var document in reader.ReadDocuments(collectionId, _select))
                {
                    yield return document;
                }
            }
        }
    }
}
