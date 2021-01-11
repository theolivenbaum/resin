﻿using System;
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
        private readonly StreamFactory _sessionFactory;
        private readonly HttpQueryParser _httpQueryParser;
        private readonly IConfigurationProvider _config;

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
                foreach (var url in documentReader.ReadDocumentValues<string>(urlCollectionId, "site"))
                {
                    collections.Add(new Uri(url).Host);
                }
                //foreach (var url in documentReader.ReadDocumentValues<string>(urlCollectionId, "page"))
                //{
                //    urls.Add(("page", url));
                //}
            }

            //if (_sessionFactory.TryGetKeyId(userDirectory, userUrlCollection, "page".ToHash(), out pageKeyId))
            //{
            //    using (var ixStream = _sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{pageKeyId}.ix")))
            //    using (var vectorStream = _sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{pageKeyId}.vec")))
            //    using (var pageIndexReader = new PageIndexReader(_sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{pageKeyId}.ixtp"))))
            //    {
            //        pageIndex = PathFinder.DeserializeTree(ixStream, vectorStream, model, pageIndexReader.Get(0).length);
            //    }
            //}

            //if (_sessionFactory.TryGetKeyId(userDirectory, userUrlCollection, "site".ToHash(), out siteKeyId))
            //{
            //    using (var ixStream = _sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{siteKeyId}.ix")))
            //    using (var vectorStream = _sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{siteKeyId}.vec")))
            //    using (var pageIndexReader = new PageIndexReader(_sessionFactory.CreateReadStream(Path.Combine(userDirectory, $"{userUrlCollection}.{siteKeyId}.ixtp"))))
            //    {
            //        siteIndex = PathFinder.DeserializeTree(ixStream, vectorStream, model, pageIndexReader.Get(0).length);
            //    }
            //}

            var query = await _httpQueryParser.ParseRequest(request, collections);

            if (query == null)
            {
                return new SearchResult(null, 0, 0, new Document[0]);
            }

#if DEBUG
            var debug = new Dictionary<string, object>();

            _httpQueryParser.ParseQuery(query, debug);

            var queryLog = JsonConvert.SerializeObject(debug);

            _logger.LogDebug($"incoming query: {queryLog}");
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