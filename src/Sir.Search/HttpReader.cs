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
    public class HttoReader : IHttpReader
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;

        public HttoReader(
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

            if (!_sessionFactory.CollectionExists(collectionId))
                return new ResponseModel { Total = 0 };

            var take = 100;
            var skip = 0;

            if (request.Query.ContainsKey("take"))
                take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                skip = int.Parse(request.Query["skip"]);

            using (var readSession = _sessionFactory.CreateReadSession(collectionId))
            {
                var query = _httpQueryParser.Parse(collectionId, request);

                if (query == null)
                {
                    return new ResponseModel { MediaType = "application/json", Total = 0 };
                }

                ReadResult result = null;

                if (request.Query.ContainsKey("id"))
                {
                    var ids = request.Query["id"].ToDictionary(s => long.Parse(s), x => (double)1);
                    var docs = readSession.ReadDocs(ids);

                    result = new ReadResult { Docs = docs, Total = docs.Count };
                }
                else
                {
                    result = readSession.Read(query, skip, take);
                }

                if (request.Query.ContainsKey("create"))
                {
                    var newCollectionName = request.Query["newCollection"].ToString();

                    if (string.IsNullOrWhiteSpace(newCollectionName))
                    {
                        newCollectionName = Guid.NewGuid().ToString();
                    }

                    _sessionFactory.Write(new Job(newCollectionName.ToHash(), result.Docs, model));
                }

                using (var mem = new MemoryStream())
                {
                    Serialize(result.Docs, mem);

                    return new ResponseModel
                    {
                        MediaType = "application/json",
                        Documents = result.Docs,
                        Total = result.Total,
                        Body = mem.ToArray()
                    };
                }
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