using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Sir.Core;
using Sir.Document;
using Sir.Search;

namespace Sir.DbUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("processing command: {0}", string.Join(" ", args));

            Logging.SendToConsole = true;

            var model = new BocModel();
            var command = args[0].ToLower();

            if (command == "submit")
            {
                var fileName = args[1];
                var uri = new Uri(args[2]);
                var count = int.Parse(args[3]);
                var batchSize = int.Parse(args[4]);
                var fullTime = Stopwatch.StartNew();
                var batchNo = 0;
                using (var httpClient = new HttpClient())
                {
                    var payload = ReadFile(fileName, 0, count)
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

                Console.WriteLine("submit took {0}", fullTime.Elapsed);
            }
            else if (command == "write")
            {
                var fullTime = Stopwatch.StartNew();
                var fileName = args[1];
                var dir = args[2];
                var collection = args[3];
                var skip = int.Parse(args[4]);
                var take = int.Parse(args[5]);
                var pageSize = int.Parse(args[6]);
                const int reportSize = 1000;
                var collectionId = collection.ToHash();
                var batchNo = 0;
                var payload = ReadFile(fileName, skip, take)
                    .Select(x => new Dictionary<string, object>
                            {
                                { "_language", x["language"].ToString() },
                                { "_url", string.Format("www.wikipedia.org/search-redirect.php?family=wikipedia&language={0}&search={1}", x["language"], x["title"]) },
                                { "title", x["title"] },
                                { "body", x["text"] }
                            });

                using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
                {
                    sessionFactory.Truncate(collectionId);

                    foreach (var page in payload.Batch(pageSize))
                    {
                        using (var writeSession = sessionFactory.CreateWriteSession(collectionId, model))
                        {
                            foreach (var batch in page.Batch(reportSize))
                            {
                                var time = Stopwatch.StartNew();

                                foreach (var document in page)
                                {
                                    writeSession.Write(document);
                                }

                                var info = writeSession.GetIndexInfo();
                                var t = time.Elapsed.TotalMilliseconds;
                                var docsPerSecond = (int)(pageSize / t * 1000);

                                foreach (var stat in info.Info)
                                {
                                    if (stat.Weight > 500)
                                        Console.WriteLine(stat);
                                }

                                Console.WriteLine(
                                    $"batch {batchNo++} took {t} ms. {docsPerSecond} docs/s");
                            }
                        }
                    }
                }

                Console.WriteLine("write operation took {0}", fullTime.Elapsed);

                Console.Read();
            }
            else if (command == "validate")
            {
                var dir = args[1];
                var collection = args[2];
                var skip = int.Parse(args[3]);
                var take = int.Parse(args[4]);
                var collectionId = collection.ToHash();
                var time = Stopwatch.StartNew();

                using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
                {
                    using (var validateSession = sessionFactory.CreateValidateSession(collectionId))
                    using (var documents = new DocumentStreamSession(new DocumentReader(collectionId, sessionFactory)))
                    {
                        foreach (var doc in documents.ReadDocs(skip, take))
                        {
                            validateSession.Validate(doc);

                            Console.WriteLine(doc["___docid"]);
                        }
                    }
                }

                Console.WriteLine("validate took {0}", time.Elapsed);

                Console.Read();
            }
            else
            {
                Console.WriteLine("unknown command: {0}", command);
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

        private static IEnumerable<IDictionary> ReadFile(string fileName, int skip, int take)
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
