using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.Core;
using Sir.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sir.CommonCrawl
{
    public static class CCHelper
    {
        public static void WriteWatSegment(
            string dataDirectory,
            string fileName,
            string collection,
            ITextModel model,
            ILogger logger,
            string refFileName)
        {
            var time = Stopwatch.StartNew();
            var collectionId = collection.ToHash();
            var storeFieldNames = new HashSet<string>
            {
                "title","description", "url", "filename"
            };
            var indexFieldNames = new HashSet<string>
            {
                "title","description", "url"
            };

            using (var sessionFactory = new SessionFactory(dataDirectory, logger))
            using (var writeSession = sessionFactory.CreateWriteSession(collectionId))
            using (var indexSession = sessionFactory.CreateIndexSession(model))
            {
                using (var queue = new ProducerConsumerQueue<IDictionary<string, object>>(1, (document =>
                {
                    sessionFactory.Write(document, writeSession, indexSession, storeFieldNames, indexFieldNames);
                })))
                {
                    foreach (var document in ReadWatFile(fileName, refFileName))
                    {
                        queue.Enqueue(document);
                    }
                }

                using (var stream = new IndexFileStreamProvider(collectionId, sessionFactory, logger))
                {
                    stream.Flush(indexSession.GetInMemoryIndex());
                }
            }

            logger.LogInformation($"indexed {fileName} in {time.Elapsed}");
        }

        public static IEnumerable<IDictionary<string, object>> ReadWatFile(string fileName, string refFileNae)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            using (var zip = new GZipStream(fs, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip, Encoding.UTF8))
            {
                var line = reader.ReadLine();

                while (line != null)
                {
                    if (line.StartsWith('{'))
                    {
                        var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                            line,
                            new JsonConverter[] { new DictionaryConverter() });

                        var envelope = (Dictionary<string, object>)doc["Envelope"];
                        var header = (Dictionary<string, object>)envelope["WARC-Header-Metadata"];
                        var type = (string)header["WARC-Type"];

                        if (type == "response")
                        {
                            var payloadMetaData = (Dictionary<string, object>)envelope["Payload-Metadata"];
                            var response = (Dictionary<string, object>)payloadMetaData["HTTP-Response-Metadata"];
                            var url = new Uri(Uri.UnescapeDataString((string)header["WARC-Target-URI"]));
                            string title = null;
                            string description = null;

                            if (response.ContainsKey("HTML-Metadata"))
                            {
                                var htmlMetaData = (Dictionary<string, object>)response["HTML-Metadata"];

                                if (htmlMetaData.ContainsKey("Head"))
                                {
                                    var head = (Dictionary<string, object>)htmlMetaData["Head"];

                                    if (head.ContainsKey("Title"))
                                    {
                                        title = (string)head["Title"];
                                    }

                                    if (head.ContainsKey("Metas"))
                                    {
                                        foreach (var meta in (IEnumerable<dynamic>)head["Metas"])
                                        {
                                            foreach (var prop in meta)
                                            {
                                                bool hasDescription = false;

                                                foreach (var x in prop)
                                                {
                                                    if (x.Value as string == "description")
                                                    {
                                                        hasDescription = true;
                                                    }
                                                }

                                                if (hasDescription && prop.Next != null)
                                                {
                                                    description = prop.Next.Value.ToString();
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            yield return new Dictionary<string, object>
                                {
                                    { "title", title },
                                    { "description", description },
                                    { "scheme", url.Scheme },
                                    { "host", url.Host },
                                    { "path", url.AbsolutePath },
                                    { "query", url.Query },
                                    { "url", url.ToString() },
                                    { "filename", refFileNae}
                                };
                        }
                    }

                    line = reader.ReadLine();
                }
            }
        }

        public static IEnumerable<string> ReadAllLinesGromGz(string fileName)
        {
            using (var stream = File.OpenRead(fileName))
            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip))
            {
                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;

                    line = reader.ReadLine();
                }
            }
        }
    }
}
