using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.CommonCrawl;
using Sir.Search;

namespace Sir.DbUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Sir.DbUtil.Program", LogLevel.Debug)
                    .AddConsole().AddDebug();
            });

            var logger = loggerFactory.CreateLogger("dbutil");

            logger.LogInformation($"processing command: {string.Join(" ", args)}");

            var model = new StringModel();
            var command = args[0].ToLower();
            var flags = ParseArgs(args);
            var plugin = ResolvePlugin(command);

            if (plugin != null)
            {
                try
                {
                    plugin.Run(flags, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            }
            else if (command == "submit")
            {
                var fullTime = Stopwatch.StartNew();

                Submit(flags);

                logger.LogInformation("submit took {0}", fullTime.Elapsed);
            }
            else if (command == "write_wp")
            {
                var fullTime = Stopwatch.StartNew();

                WriteWP(flags, model, loggerFactory);

                logger.LogInformation("write operation took {0}", fullTime.Elapsed);
            }
            else if ((command == "slice"))
            {
                Slice(flags);
            }
            else if (command == "truncate")
            {
                Truncate(flags["collection"], logger);
            }
            else if (command == "truncate-index")
            {
                TruncateIndex(flags["collection"], logger);
            }
            else if (command == "optimize")
            {
                Optimize(flags, model, logger);
            }
            else
            {
                logger.LogInformation("unknown command: {0}", command);
            }

            logger.LogInformation($"executed {command}");
        }

        private static ICommand ResolvePlugin(string command)
        {
            var reader = new PluginReader(Directory.GetCurrentDirectory());
            var plugins = reader.Read<ICommand>("command");

            if (!plugins.ContainsKey(command))
                return null;

            return plugins[command];
        }

        private static IDictionary<string, string> ParseArgs(string[] args)
        {
            var dic = new Dictionary<string, string>();

            for (int i = 1; i < args.Length; i += 2)
            {
                dic.Add(args[i].Replace("-", ""), args[i + 1]);
            }

            return dic;
        }

        /// <summary>
        /// Required args: collection, skip, take, batchSize
        /// </summary>
        private static void Optimize(IDictionary<string, string> args, StringModel model, ILogger logger)
        {
            var collection = args["collection"];
            var skip = int.Parse("skip");
            var take = int.Parse("take");
            var batchSize = int.Parse("batchSize");

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), logger))
            {
                sessionFactory.Optimize(
                    collection, 
                    new HashSet<string> { "title", "description", "url", "filename" },
                    new HashSet<string> { "title", "description", "url" },
                    model,
                    skip,
                    take,
                    batchSize);
            }
        }

        /// <summary>
        /// Required args: sourceFileName, resultFileName, length
        /// </summary>
        private static void Slice(IDictionary<string, string> args)
        {
            var file = args["sourceFileName"];
            var slice = args["resultFileName"];
            var len = int.Parse(args["length"]);

            Span<byte> buf = new byte[len];

            using (var fs = File.OpenRead(file))
            using (var target = File.Create(slice))
            {
                fs.Read(buf);
                target.Write(buf);
            }
        }

        private static void WriteWP(IDictionary<string, string> args, IStringModel model, ILoggerFactory log)
        {
            //var fileName = args[1];
            //var dir = args[2];
            //var collection = args[3];
            //var skip = int.Parse(args[4]);
            //var take = int.Parse(args[5]);
            //var pageSize = int.Parse(args[6]);
            //const int reportSize = 1000;
            //var collectionId = collection.ToHash();
            //var batchNo = 0;
            //var payload = ReadWP(fileName, skip, take)
            //    .Select(x => new Dictionary<string, object>
            //            {
            //                    { "_language", x["language"].ToString() },
            //                    { "_url", string.Format("www.wikipedia.org/search-redirect.php?family=wikipedia&language={0}&search={1}", x["language"], x["title"]) },
            //                    { "title", x["title"] },
            //                    { "body", x["text"] }
            //            });

            //using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model, log))
            //{
            //    foreach (var page in payload.Batch(pageSize))
            //    {
            //        using (var writeSession = sessionFactory.CreateWriteSession(collectionId, model))
            //        {
            //            foreach (var batch in page.Batch(reportSize))
            //            {
            //                var time = Stopwatch.StartNew();

            //                foreach (var document in page)
            //                {
            //                    writeSession.Write(document);
            //                }

            //                var info = writeSession.GetIndexInfo();
            //                var t = time.Elapsed.TotalMilliseconds;
            //                var docsPerSecond = (int)(pageSize / t * 1000);

            //                foreach (var stat in info.Info)
            //                {
            //                    if (stat.Weight > 500)
            //                        Console.WriteLine(stat);
            //                }

            //                Console.WriteLine(
            //                        $"batch {batchNo++} took {t} ms. {docsPerSecond} docs/s");
            //            }
            //        }
            //    }
            //}
        }

        /// <summary>
        /// Required args: collection
        /// </summary>
        private static void Truncate(string collection, ILogger log)
        {
            var collectionId = collection.ToHash();

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), log))
            {
                sessionFactory.Truncate(collectionId);
            }
        }

        /// <summary>
        /// Required args: collection
        /// </summary>
        private static void TruncateIndex(string collection, ILogger log)
        {
            var collectionId = collection.ToHash();

            using (var sessionFactory = new SessionFactory(new KeyValueConfiguration("sir.ini"), log))
            {
                sessionFactory.TruncateIndex(collectionId);
            }
        }

        /// <summary>
        /// Required args: fileName, uri, count, batchSize
        /// </summary>
        private static void Submit(IDictionary<string, string> args)
        {
            var fileName = args["fileName"];
            var uri = new Uri(args["uri"]);
            var count = int.Parse(args["count"]);
            var batchSize = int.Parse(args["batchSize"]);
            var batchNo = 0;
            using (var httpClient = new HttpClient())
            {
                var payload = ReadWP(fileName, 0, count)
                                        .Select(x => new Dictionary<string, object>
                                                {
                                { "_language", x["language"].ToString() },
                                { "_url", string.Format("www.wikipedia.org/search-redirect.php?family=wikipedia&language={0}&search={1}", x["language"], x["title"]) },
                                { "title", x["title"] },
                                { "body", x["text"] }
                                                });

                foreach (var batch in payload.Batch(batchSize))
                {
                    var time = Stopwatch.StartNew();
                    Submit(batch, uri, httpClient);
                    time.Stop();
                    var docsPerSecond = (int)(batchSize / time.Elapsed.TotalSeconds);
                    Console.WriteLine($"batch {batchNo++} took {time.Elapsed} {docsPerSecond} docs/s");
                }
            }
        }

        private static IEnumerable<IDictionary> ReadGZipJsonFile(string fileName, int skip, int take)
        {
            var skipped = 0;
            var took = 0;

            using (var stream = File.OpenRead(fileName))
            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip))
            {
                //skip first line
                reader.ReadLine();

                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("]"))
                {
                    if (took == take)
                        break;

                    if (skipped++ < skip)
                    {
                        continue;
                    }

                    var doc = JsonConvert.DeserializeObject<IDictionary>(line);

                    if (doc.Contains("title"))
                    {
                        yield return doc;
                        took++;
                    }

                    line = reader.ReadLine();
                }
            }
        }

        private static IEnumerable<IDictionary> ReadWP(string fileName, int skip, int take)
        {
            if (Path.GetExtension(fileName).EndsWith("gz"))
            {
                return ReadGZipJsonFile(fileName, skip, take);
            }

            return ReadJsonFile(fileName, skip, take);
        }

        private static IEnumerable<IDictionary> ReadJsonFile(string fileName, int skip, int take)
        {
            var skipped = 0;
            var took = 0;

            using (var stream = File.OpenRead(fileName))
            using (var reader = new StreamReader(stream))
            {
                //skip first line
                reader.ReadLine();

                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("]"))
                {
                    if (took == take)
                        break;

                    if (skipped++ < skip)
                    {
                        continue;
                    }

                    var doc = JsonConvert.DeserializeObject<IDictionary>(line);

                    if (doc.Contains("title"))
                    {
                        yield return doc;
                        took++;
                    }

                    line = reader.ReadLine();
                }
            }
        }

        private static void Submit(IEnumerable<IDictionary> documents, Uri uri, HttpClient client)
        {
            var jsonStr = JsonConvert.SerializeObject(documents);
            var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
            var response = client.PostAsync(uri, content).Result;

            response.EnsureSuccessStatusCode();
        }

        private static void Serialize(IEnumerable<object> docs, Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, docs);
                jsonWriter.Flush();
            }
        }
    }
}

