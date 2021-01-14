using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using Sir.VectorSpace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sir.Crawl
{
    public class CrawlUserDirectoryCommand : ICommand
    {
        private readonly HashSet<string> _select = new HashSet<string> { "page", "site", "last_crawl_date" };
        private readonly HashSet<string> _history = new HashSet<string>();
        private readonly IModel<string> _model = new BagOfCharsModel();

        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var userDirectory = args["userDirectory"];
            var urlCollectionId = "url".ToHash();
            var htmlClient = new HtmlWeb();

            //htmlClient.UserAgent = "Crawlcrawler (+https://crawlcrawler.com)";
            htmlClient.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36";
            
            using (var database = new StreamFactory(logger))
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
                            var timeOfCrawl = DateTime.Now;
                            var documents = DoCrawl(uri, htmlClient, siteWide);

                            using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, database)))
                            using (var indexSession = new IndexSession<string>(_model, _model))
                            {
                                foreach (var document in documents)
                                {
                                    database.Write(document, writeSession, indexSession);
                                }
                            }

                            using (var updateSession = new UpdateSession(userDirectory, urlCollectionId, database))
                            {
                                updateSession.Update(url.Id, lastCrawlDateKeyId, timeOfCrawl);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Crawl error");
                        }
                    }
                }
            }
        }

        private IEnumerable<Document> DoCrawl(Uri uri, HtmlWeb htmlClient, bool siteWide)
        {
            if (!_history.Add(uri.ToString()))
            {
                yield break;
            }

            var doc = htmlClient.Load(uri);
            var title = doc.DocumentNode.Descendants("title").FirstOrDefault().InnerText;
            var sb = new StringBuilder();

            foreach (var node in doc.DocumentNode.DescendantsAndSelf())
            {
                if (!node.HasChildNodes)
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
                new Field("url", uri.ToString()),
                new Field("host", uri.Host)
            });

            if (siteWide)
            {
                foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    var linkUri = new Uri(link.Attributes["href"].Value);

                    if (!_history.Add(linkUri.ToString()))
                    {
                        continue;
                    }

                    if (linkUri.Host == uri.Host)
                    {
                        foreach (var document in DoCrawl(linkUri, htmlClient, siteWide: false))
                        {
                            yield return document;

                            Thread.Sleep(1000);
                        }
                    }
                }
            }
        }

        private IEnumerable<Document> Urls(string directory, ulong collectionId, StreamFactory streamFactory)
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
