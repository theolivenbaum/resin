using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Sir.Search;

namespace Sir.HttpServer
{
    /// <summary>
    /// Write to a collection.
    /// </summary>
    public class HttpWriter : IHttpWriter
    {
        private readonly SessionFactory _sessionFactory;

        public HttpWriter(SessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public void Write(HttpRequest request, ITextModel model)
        {
            var documents = Deserialize<IEnumerable<Document>>(request.Body);
            var collectionId = request.Query["collection"].First().ToHash();

            _sessionFactory.Write(
                new WriteJob(
                    collectionId,
                    documents,
                    model));
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