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

namespace Sir.Crawl
{
    public class CrawlUserCollectionsCommand : ICommand
    {
        private readonly HashSet<string> _select = new HashSet<string> { "page", "site", "last_crawl_date" };
        private readonly HashSet<ulong> _history = new HashSet<ulong>();
        private readonly IModel<string> _model = new BagOfCharsModel();

        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var dataDirectory = args["dataDirectory"];
            var userDirectory = args["userDirectory"];
            var htmlClient = new HtmlWeb();

            htmlClient.UserAgent = "Crawlcrawler (+https://crawlcrawler.com)";
            
            using (var streamFactory = new StreamFactory(logger))
            {
                foreach (var url in Urls(userDirectory, streamFactory))
                {
                    var siteWide = url.StartsWith("site://");
                    var uri = new Uri(url.Replace("site://", "https://").Replace("page://", "https://"));
                    var collectionId = uri.Host.ToHash();

                    try
                    {
                        var documents = DoCrawl(uri, htmlClient, siteWide);

                        using (var writeSession = new WriteSession(new DocumentWriter(dataDirectory, collectionId, streamFactory)))
                        using (var indexSession = new IndexSession<string>(_model, _model))
                        {
                            foreach (var document in documents)
                            {
                                streamFactory.Write(document, writeSession, indexSession);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Crawl error");
                    }
                }
            }
        }

        private IEnumerable<Document> DoCrawl(Uri uri, HtmlWeb htmlClient, bool siteWide)
        {
            if (!_history.Add(uri.ToString().ToHash()))
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
        }

        private IEnumerable<string> Urls(string userDirectory, StreamFactory streamFactory)
        {
            var collectionId = "url".ToHash();

            foreach (var directory in Directory.EnumerateDirectories(userDirectory))
            {
                using (var reader = new DocumentStreamSession(directory, streamFactory))
                {
                    foreach (var document in reader.ReadDocuments(collectionId, _select))
                    {
                        string page = null;
                        string site = null;
                        //DateTime lastCrawlDate = DateTime.Now;

                        foreach (var field in document.Fields)
                        {
                            if (field.Name == "page")
                            {
                                page = (string)field.Value;
                            }
                            else if (field.Name == "site")
                            {
                                site = (string)field.Value;
                            }
                            //else
                            //{
                            //    lastCrawlDate = (DateTime)field.Value;
                            //}
                        }

                        if (site != null)
                            yield return site.Replace("https://", "site://");
                        else
                            yield return page.Replace("https://", "page://");
                    }
                }
            }
        }
    }
}
