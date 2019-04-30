using System;
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
        private readonly ProducerConsumerQueue<Job> _writer;

        public StoreWriter(SessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _timer = new Stopwatch();
            _writer = new ProducerConsumerQueue<Job>(1, callback: ExecuteWrite);
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
            var job = new Job(collectionName, documents);

            _writer.Enqueue(job);

            while (!job.Done)
            {
                Thread.Sleep(10);
            }

            return new ResponseModel();
        }

        public void Push(Job job)
        {
            _writer.Enqueue(job);
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
            _writer.Dispose();
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