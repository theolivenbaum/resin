using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.Search;
using Sir.VectorSpace;

namespace Sir.HttpServer
{
    /// <summary>
    /// Query a collection.
    /// </summary>
    public class HttpReader : IHttpReader
    {
        public string ContentType => "application/json";

        private readonly ILogger<HttpReader> _logger;
        private readonly SessionFactory _sessionFactory;
        private readonly HttpStringQueryParser _httpQueryParser;

        public HttpReader(
            SessionFactory sessionFactory, 
            HttpStringQueryParser httpQueryParser,
            ILogger<HttpReader> logger)
        {
            _logger = logger;
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
        }

        public void Dispose()
        {
        }

        public async Task<ResponseModel> Read(HttpRequest request, ITextModel model)
        {
            var timer = Stopwatch.StartNew();
            var take = 100;
            var skip = 0;

            if (request.Query.ContainsKey("take"))
                take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                skip = int.Parse(request.Query["skip"]);

            IQuery query = await _httpQueryParser.ParseRequest(request);

            if (query == null)
            {
                return new ResponseModel { MediaType = "application/json", Total = 0 };
            }

#if DEBUG
            var debug = new Dictionary<string, object>();

            _httpQueryParser.ParseQuery(query, debug);

            _logger.LogInformation(JsonConvert.SerializeObject(debug));
#endif

            using (var readSession = _sessionFactory.CreateQuerySession(model))
            {
                var result = readSession.Search(query, skip, take);

                using (var mem = new MemoryStream())
                {
                    Serialize(result.Documents, mem);

                    return new ResponseModel
                    {
                        MediaType = "application/json",
                        Documents = result.Documents,
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