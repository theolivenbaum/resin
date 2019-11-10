using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Write into a collection.
    /// </summary>
    public class HttpWriter : IHttpWriter, ILogger
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly Stopwatch _timer;

        public HttpWriter(SessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _timer = new Stopwatch();
        }

        public void Write(HttpRequest request, IStringModel model)
        {
            var documents = Deserialize<IEnumerable<IDictionary<string, object>>>(request.Body);

            if (request.Query.ContainsKey("collection"))
            {
                var collections = request.Query["collection"].ToArray();

                foreach (var collection in collections)
                {
                    _sessionFactory.WriteConcurrent(new Job(collection.ToHash(), documents, model));
                }
            }
            else
            {
                _sessionFactory.WriteConcurrent(documents, model);
            }
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

        public void Dispose()
        {
        }
    }
}