using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Sir.Search;
using Sir.VectorSpace;

namespace Sir.HttpServer
{
    /// <summary>
    /// Write to a collection.
    /// </summary>
    public class HttpWriter : IHttpWriter
    {
        private readonly Database _sessionFactory;
        private readonly IConfigurationProvider _config;

        public HttpWriter(Database sessionFactory, IConfigurationProvider config)
        {
            _sessionFactory = sessionFactory;
            _config = config;
        }

        public void Write(HttpRequest request, IModel<string> model)
        {
            var documents = Deserialize<IEnumerable<Document>>(request.Body);
            var collectionId = request.Query["collection"].First().ToHash();

            _sessionFactory.StoreIndexAndWrite(
                _config.Get("data_dir"),
                collectionId,
                documents,
                model);
        }

        private static T Deserialize<T>(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = new JsonSerializer();
                return ser.Deserialize<T>(jsonReader);
            }
        }
    }
}