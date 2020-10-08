using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace Sir.Wikipedia
{
    public class SubmitWikipediaCommand : ICommand
    {
        public void Run(IDictionary<string, string> args, ILogger logger)
        {
            var fileName = args["fileName"];
            var uri = new Uri(args["uri"]);
            var count = int.Parse(args["count"]);
            var batchSize = int.Parse(args["batchSize"]);
            var batchNo = 0;
            using (var httpClient = new HttpClient())
            {
                var payload = WikipediaHelper.ReadWP(fileName, 0, count)
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

        private static void Submit(IEnumerable<IDictionary> documents, Uri uri, HttpClient client)
        {
            var jsonStr = JsonConvert.SerializeObject(documents);
            var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
            var response = client.PostAsync(uri, content).Result;

            response.EnsureSuccessStatusCode();
        }
    }
}
