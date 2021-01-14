using System;
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
        private readonly ILogger<HttpReader> _logger;
        private readonly StreamFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;
        private readonly IConfigurationProvider _config;
        private readonly string[] _fields;
        private readonly string[] _select;

        public HttpReader(
            StreamFactory sessionFactory, 
            HttpQueryParser httpQueryParser,
            IConfigurationProvider config,
            ILogger<HttpReader> logger)
        {
            _logger = logger;
            _sessionFactory = sessionFactory;
            _httpQueryParser = httpQueryParser;
            _config = config;
            _fields = _config.GetMany("default_fields");
            _select = _config.GetMany("default_select_fields");
        }

        public async Task<SearchResult> Read(HttpRequest request, IModel<string> model)
        {
            var timer = Stopwatch.StartNew();
            var take = 100;
            var skip = 0;

            if (request.Query.ContainsKey("take"))
                take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                skip = int.Parse(request.Query["skip"]);

            var queryId = request.Query["queryId"].ToString();
            var userDirectory = Path.Combine(_config.Get("user_dir"), queryId);
            var urlCollectionId = "url".ToHash();
            var collections = new List<string>();

            using (var documentReader = new DocumentStreamSession(userDirectory, _sessionFactory))
            {
                foreach (var url in documentReader.ReadDocumentValues<string>(urlCollectionId, "host"))
                {
                    collections.Add(url);
                }
            }

            var query = await _httpQueryParser.ParseRequest(request, collections, _fields, _select);

            if (query == null)
            {
                return new SearchResult(null, 0, 0, new Document[0]);
            }

#if DEBUG
            var debug = new Dictionary<string, object>();

            _httpQueryParser.ParseQuery(query, debug);

            var queryLog = JsonConvert.SerializeObject(debug);

            _logger.LogDebug($"parsed query: {queryLog}");
#endif

            using (var readSession = new SearchSession(_config.Get("data_dir"), _sessionFactory, model, _logger))
            {
                return readSession.Search(query, skip, take);
            }
        }

        public void Dispose()
        {
        }
    }
}