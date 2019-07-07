using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
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

            if (command == "query")
            {
                // example: query C:\projects\resin\src\Sir.HttpServer\App_Data www

                Query(
                    dir: args[1], 
                    collectionName: args[2],
                    model);
            }
            else if (command == "index")
            {
                // example: index www

                Index(
                    dir: args[1],
                    collectionName: args[2],
                    model: model);
            }
            else if (command == "validate")
            {
                // example: validate C:\projects\resin\src\Sir.HttpServer\App_Data www 0 3000

                Validate(
                    dir: args[1], 
                    collectionName: args[2], 
                    skip: int.Parse(args[3]), 
                    take: int.Parse(args[4]),
                    model);
            }
            else if (command == "warmup")
            {
                // example: warmup C:\projects\resin\src\Sir.HttpServer\App_Data https://didyougogo.com www 0 3000

                var dir = args[1];
                var uri = new Uri(args[2]);
                var collection = args[3];
                var skip = int.Parse(args[4]);
                var take = int.Parse(args[5]);

                Warmup(
                    dir, 
                    uri, 
                    collection, 
                    skip, 
                    take,
                    new CbocModel());
            }
            else if (command == "submit")
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
                var batchSize = int.Parse(args[6]);

                var fullTime = Stopwatch.StartNew();
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

                var collectionId = collection.ToHash();
                using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
                {
                    sessionFactory.Truncate(collectionId);

                    using (var writeSession = sessionFactory.CreateWriteSession(
                        collection,
                        collectionId,
                        model))
                    {
                        var time = Stopwatch.StartNew();

                        foreach (var batch in payload.Batch(batchSize))
                        {
                            writeSession.Write(batch);

                            var t = time.Elapsed.TotalMilliseconds;

                            var docsPerSecond = (int)(batchSize / t*1000);

                            Console.WriteLine($"batch {batchNo++} took {t} ms, {docsPerSecond} docs/s");

                            time.Restart();
                        }
                    }
                }

                Console.WriteLine("write operation took {0}", fullTime.Elapsed);

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

        private static void Warmup(string dir, Uri uri, string collectionName, int skip, int take, IStringModel model)
        {
            using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                {
                    using (var session = sessionFactory.CreateWarmupSession(collectionName, collectionName.ToHash(), uri.ToString()))
                    {
                        session.Warmup(documentStreamSession.ReadDocs(skip, take), 0);
                    }
                }
            }
        }

        private static void Index(string dir, string collectionName, IStringModel model)
        {
            using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
            {
                var collectionId = collectionName.ToHash();

                sessionFactory.TruncateIndex(collectionId);

                using (var indexSession = sessionFactory.CreateIndexSession(collectionName, collectionId))
                using (var documents = sessionFactory.CreateDocumentStreamSession(collectionName, collectionId))
                {
                    documents.Index(indexSession, Console.Out);
                }
            }
        }

        private static void Query(string dir, string collectionName, IStringModel model)
        {
            var tokenizer = new CbocModel();
            var qp = new QueryParser();
            var sessionFactory = new SessionFactory(
                new IniConfiguration(Path.Combine(Directory.GetCurrentDirectory(), "sir.ini")), model);

            while (true)
            {
                Console.Write("query>");

                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input == "q" || input == "quit")
                {
                    break;
                }

                var q = qp.Parse(collectionName.ToHash(), input, tokenizer);
                q.Skip = 0;
                q.Take = 100;

                using (var session = sessionFactory.CreateReadSession(collectionName, collectionName.ToHash()))
                {
                    var result = session.Read(q);
                    var docs = result.Docs;

                    var index = 0;

                    foreach (var doc in docs.Take(10))
                    {
                        Console.WriteLine("{0} {1} {2}", index++, doc["___score"], doc["title"]);
                    }
                }
            }
        }

        private static void Validate(string dir, string collectionName, int skip, int take, IStringModel model)
        {
            var time = Stopwatch.StartNew();

            using (var sessionFactory = new SessionFactory(new IniConfiguration("sir.ini"), model))
            {
                using (var documentStreamSession = sessionFactory.CreateDocumentStreamSession(collectionName, collectionName.ToHash()))
                using (var validateSession = sessionFactory.CreateValidateSession(collectionName, collectionName.ToHash()))
                {
                    validateSession.Validate(documentStreamSession.ReadDocs(skip, take));
                }
            }

            Logging.Log(null, string.Format("{0} validate operation took {1}", collectionName, time.Elapsed));
        }
    }
}
