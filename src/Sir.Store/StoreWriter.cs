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

        public async Task<ResponseModel> Write(string collectionId, HttpRequest request)
        {
            _timer.Restart();

            var payload = new MemoryStream();

            await request.Body.CopyToAsync(payload);

            if (request.ContentLength.Value != payload.Length)
            {
                throw new DataMisalignedException();
            }

            payload.Position = 0;

            var data = Deserialize<IEnumerable<IDictionary>>(payload);
            var job = new WriteJob(collectionId, data);

            this.Log("deserialized write job {0} for collection {1} in {2}", job.Id, collectionId, _timer.Elapsed);

            var docIds = await ExecuteWrite(job);
            var response = new MemoryStream();

            Serialize(docIds, response);

            return new ResponseModel { Stream = response, MediaType = "application/json" };
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

        private async Task<IList<long>> ExecuteWrite(WriteJob job)
        {
            try
            {
                _timer.Restart();

                IList<long> docIds;

                using (var write = _sessionFactory.CreateWriteSession(job.CollectionName, job.CollectionName.ToHash()))
                {
                    docIds = await write.Write(job);
                }

                using (var index = _sessionFactory.CreateIndexSession(
                    job.CollectionName, job.CollectionName.ToHash()))
                {
                    foreach (var doc in job.Documents)
                    {
                        index.EmbedTerms(doc);
                    }
                }

                this.Log("executed write+index job {0} in {1}", job.Id, _timer.Elapsed);

                return docIds;
            }
            catch (Exception ex)
            {
                this.Log("failed to write job {0}: {1}", job.Id, ex);

                throw;
            }
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