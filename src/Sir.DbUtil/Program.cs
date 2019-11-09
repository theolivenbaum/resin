using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Sir.Document;
using Sir.Store;

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
                var httpClient = new HttpClient();
                var payload = ReadFile(fileName)
                    .Skip(0)
                    .Take(count)
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

                Console.WriteLine("submit took {0}", fullTime.Elapsed);
            }
            else if (command == "write")
            {
                var fileName = args[1];
                var dir = args[2];
                var collection = args[3];
                var skip = int.Parse(args[4]);
                var take = int.Parse(args[5]);
                var pageSize = int.Parse(args[6]);
                const int reportSize = 1000;
                var collectionId = collection.ToHash();
                var batchNo = 0;
                var payload = ReadFile(fileName)
                    .Skip(skip)
                    .Take(take)
                    .Select(x => new Dictionary<string, object>
                            {
                                { "_language", x["language"].ToString() },
                                { "_url", string.Format("www.wikipedia.org/search-redirect.php?family=wikipedia&language={0}&search={1}", x["language"], x["title"]) },
                                { "title", x["title"] },
                                { "body", x["text"] }
                            });

                var fullTime = Stopwatch.StartNew();

                using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
                {
                    sessionFactory.Truncate(collectionId);

                    foreach (var page in payload.Batch(pageSize))
                    {
                        using (var writeSession = sessionFactory.CreateWriteSession(collectionId, model))
                        {
                            var time = Stopwatch.StartNew();

                            foreach (var batch in page.Batch(reportSize))
                            {
                                var info = writeSession.Write(batch);

                                var t = time.Elapsed.TotalMilliseconds;

                                var docsPerSecond = (int)(reportSize / t * 1000);
                                var segments = 0;

                                foreach (var stat in info.Info)
                                {
                                    if (stat.Weight > 500)
                                        Console.WriteLine(stat);

                                    segments++;
                                }

                                Console.WriteLine($"batch {batchNo++} took {t} ms. {segments} segments. {docsPerSecond} docs/s");

                                time.Restart();
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
                        foreach (var doc in documents.ReadDocs(collectionId, skip, take))
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

        private static IEnumerable<IDictionary> ReadFile(string fileName)
        {
            using (var stream = File.OpenRead(fileName))
            using (var reader = new StreamReader(stream))
            {
                //skip first line
                reader.ReadLine();

                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("]"))
                {
                    var doc = JsonConvert.DeserializeObject<IDictionary>(line);

                    if (doc.Contains("title"))
                    {
                        yield return doc;
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
