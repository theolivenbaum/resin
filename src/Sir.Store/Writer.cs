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
    public class Writer : IWriter
    {
        public string ContentType => "*";

        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Stopwatch _timer;

        public Writer(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _log = Logging.CreateWriter("writer");
            _timer = new Stopwatch();
        }

        public async Task<long> Write(string collectionId, Stream payload)
        {
            try
            {
                _timer.Restart();

                var data = Deserialize<IEnumerable<IDictionary>>(payload);
                var job = new WriteJob(collectionId, data);

                _log.Log(string.Format("deserialized write job {0} for collection {1} in {2}", job.Id, collectionId, _timer.Elapsed));

                return await ExecuteWrite(job);
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("write failed: {0}", ex));

                throw;
            }
        }

        public async Task Write(string collectionId, long id, Stream payload)
        {
            throw new NotImplementedException();
        }

        private async Task<long> ExecuteWrite(WriteJob job)
        {
            try
            {
                _timer.Restart();

                Task<ulong> lastProcessedDocId;

                using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                {
                    lastProcessedDocId = session.Write(job.Documents);
                }

                _log.Log(string.Format("executed write job {0} in {1}", job.Id, _timer.Elapsed));

                var id = await lastProcessedDocId;

                return Convert.ToInt64(id);
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