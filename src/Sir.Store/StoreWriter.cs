using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Sir.Core;

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
            var documents = Deserialize<IEnumerable<IDictionary>>(request.Body);
            var job = new Job(collectionName, documents);

            await _sessionFactory.Write(job);

            return new ResponseModel();
        }

        private static void Serialize(object value, Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 4096, true))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, value);
            }
            stream.Position = 0;
        }

        public async Task ExecuteWrite(Job job)
        {
            _timer.Restart();

            var colId = job.Collection.ToHash();

            using (var writeSession = _sessionFactory.CreateWriteSession(job.Collection, colId))
            using (var indexSession = _sessionFactory.CreateIndexSession(job.Collection, colId))
            {
                foreach (var doc in job.Documents)
                {
                    await writeSession.Write(doc);
                    indexSession.Index(doc);
                }
            }

            job.Done = true;

            this.Log("executed {0} write+index job in {1}", job.Collection, _timer.Elapsed);
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

    public class Job
    {
        public string Collection { get; private set; }
        public IEnumerable<IDictionary> Documents { get; private set; }
        public bool Done { get; set; } 

        public Job(string collection, IEnumerable<IDictionary> documents)
        {
            Collection = collection;
            Documents = documents;
        }
    }
}