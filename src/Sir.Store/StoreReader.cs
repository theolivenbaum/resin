using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Query a collection.
    /// </summary>
    public class StoreReader : IReader
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;

        public StoreReader(
            SessionFactory sessionFactory, 
            HttpQueryParser httpQueryParser)
        {
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
        }

        public void Dispose()
        {
        }

        public ResponseModel Read(string collectionName, IStringModel model, HttpRequest request)
        {
            var timer = Stopwatch.StartNew();
            var collectionId = collectionName.ToHash(); 

            using (var readSession = _sessionFactory.CreateReadSession(collectionId))
            {
                var query = _httpQueryParser.Parse(collectionId, model, request);

                if (query == null)
                {
                    return new ResponseModel { MediaType = "application/json", Total = 0 };
                }

                var result = readSession.Read(query);
                long total = result.Total;

                if (request.Query.ContainsKey("create"))
                {
                    var newCollectionName = request.Query["newCollection"].ToString();

                    if (string.IsNullOrWhiteSpace(newCollectionName))
                    {
                        newCollectionName = Guid.NewGuid().ToString();
                    }

                    _sessionFactory.Write(new Job(newCollectionName.ToHash(), result.Docs, model));
                }

                var mem = new MemoryStream();

                Serialize(result.Docs, mem);

                return new ResponseModel
                {
                    MediaType = "application/json",
                    Documents = result.Docs,
                    Total = total,
                    Body = mem.ToArray()
                };
            }
        }

        private void Serialize(IEnumerable<IDictionary<string, object>> docs, Stream stream)
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