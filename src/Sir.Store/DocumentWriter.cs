using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Sir.Store
{
    /// <summary>
    /// Write into a document collection.
    /// </summary>
    public class DocumentWriter : IWriter
    {
        public string ContentType => "application/json";

        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Stopwatch _timer;

        public DocumentWriter(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _log = Logging.CreateWriter("documentwriter");
            _timer = new Stopwatch();
        }

        public async Task<Result> Write(string collectionId, Stream payload)
        {
            try
            {
                _timer.Restart();

                var data = Deserialize<IEnumerable<IDictionary>>(payload);
                var job = new WriteJob(collectionId, data);

                _log.Log(string.Format("deserialized write job {0} for collection {1} in {2}", job.Id, collectionId, _timer.Elapsed));

                var docIds = await ExecuteWrite(job);
                var response = new MemoryStream();

                Serialize(docIds, response);

                return new Result { Data = response, MediaType = "application/json"};
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("write failed: {0}", ex));

                throw;
            }
        }

        private static void Serialize(object value, Stream s)
        {
            using (StreamWriter writer = new StreamWriter(s))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, value);
            }
            s.Position = 0;
        }

        private async Task<IList<ulong>> ExecuteWrite(WriteJob job)
        {
            try
            {
                _timer.Restart();

                IList<ulong> docIds;

                using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                {
                    docIds = await session.Write(job.Documents);
                }

                _log.Log(string.Format("executed write job {0} in {1}", job.Id, _timer.Elapsed));

                return docIds;
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("failed to write job {0}: {1}", job.Id, ex));

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
            _log.Dispose();
        }
    }
}