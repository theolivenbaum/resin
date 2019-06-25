using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Query a collection.
    /// </summary>
    public class StoreReader : IReader, ILogger
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;
        private readonly HttpBowQueryParser _httpBowQueryParser;

        public StoreReader(
            SessionFactory sessionFactory, 
            HttpQueryParser httpQueryParser, 
            HttpBowQueryParser httpDocumentQueryParser)
        {
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
            _httpBowQueryParser = httpDocumentQueryParser;
        }

        public void Dispose()
        {
        }

        public ResponseModel Read(string collectionName, IStringModel model, HttpRequest request)
        {
            var timer = Stopwatch.StartNew();
            var collectionId = collectionName.ToHash(); 

            using (var session = _sessionFactory.CreateReadSession(collectionName, collectionId))
            {
                var query = _httpQueryParser.Parse(collectionId, model, request);

                if (query == null)
                {
                    return new ResponseModel { MediaType = "application/json", Total = 0 };
                }

                var result = session.Read(query);

                IList<IDictionary<string, object>> docs = result.Docs;
                long total = result.Total;

                this.Log(string.Format("executed query {0} in {1}", query, timer.Elapsed));

                if (request.Query.ContainsKey("create"))
                {
                    var newCollectionName = request.Query["newCollection"].ToString();

                    if (string.IsNullOrWhiteSpace(newCollectionName))
                    {
                        newCollectionName = Guid.NewGuid().ToString();
                    }

                    _sessionFactory.ExecuteWrite(new Job(newCollectionName, docs, model));
                }

                var mem = new MemoryStream();

                Serialize(docs, mem);

                return new ResponseModel
                {
                    MediaType = "application/json",
                    Documents = docs,
                    Total = total,
                    Body = mem.ToArray()
                };
            }
        }

        private void Serialize(IList<IDictionary<string, object>> docs, Stream stream)
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