using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.Search;

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
        private readonly HttpQueryParser _httpQueryParser;

        public HttpReader(
            SessionFactory sessionFactory, 
            HttpQueryParser httpQueryParser,
            ILogger<HttpReader> logger)
        {
            _logger = logger;
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
        }

        public void Dispose()
        {
        }

        public async Task<SearchResult> Read(HttpRequest request, ITextModel model)
        {
            var timer = Stopwatch.StartNew();
            var take = 100;
            var skip = 0;

            if (request.Query.ContainsKey("take"))
                take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                skip = int.Parse(request.Query["skip"]);

            var query = await _httpQueryParser.ParseRequest(request);

            if (query == null)
            {
                return new SearchResult(null, 0, 0, new Document[0]);
            }

#if DEBUG
            var debug = new Dictionary<string, object>();

            _httpQueryParser.ParseQuery(query, debug);

            _logger.LogInformation(JsonConvert.SerializeObject(debug));
#endif

            using (var readSession = _sessionFactory.CreateQuerySession(model))
            {
                return readSession.Search(query, skip, take);
            }
        }
    }
}