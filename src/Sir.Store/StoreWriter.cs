using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Write into a collection.
    /// </summary>
    public class StoreWriter : IWriter, ILogger
    {
        public string ContentType => "application/json";

        private readonly SessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly Stopwatch _timer;

        public StoreWriter(SessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _timer = new Stopwatch();
        }

        public async Task<ResponseModel> Write(string collectionName, HttpRequest request)
        {
            var payload = new MemoryStream();

            await request.Body.CopyToAsync(payload);

            if (request.ContentLength.Value != payload.Length)
            {
                throw new DataMisalignedException();
            }

            payload.Position = 0;

            var documents = Deserialize<IEnumerable<IDictionary>>(payload);

            await ExecuteWrite(collectionName, documents);

            return new ResponseModel();
        }

        private static void Serialize(object value, Stream s)
        {
            using (StreamWriter writer = new StreamWriter(s, Encoding.UTF8, 4096, true))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, value);
            }
            s.Position = 0;
        }

        public async Task ExecuteWrite(string collection, IEnumerable<IDictionary> documents)
        {
            _timer.Restart();

            using (var writeSession = _sessionFactory.CreateWriteSession(collection, collection.ToHash()))
            using (var indexSession = _sessionFactory.CreateIndexSession(collection, collection.ToHash()))
            {
                foreach (var doc in documents)
                {
                    await writeSession.Write(doc);
                    indexSession.Index(doc);
                }
            }

            this.Log("executed {0} write+index job in {1}", collection, _timer.Elapsed);
        }

        private static T Deserialize<T>(Stream s)
        {
            using (StreamReader reader = new StreamReader(s))
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