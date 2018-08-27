using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Write into a document collection.
    /// </summary>
    public class Writer : IWriter
    {
        public string ContentType => "*";

        private readonly ProducerConsumerQueue<WriteJob> _writeQueue;
        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Stopwatch _itemTimer;
        private readonly Stopwatch _batchTimer;

        public Writer(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _writeQueue = new ProducerConsumerQueue<WriteJob>(ExecuteWrite);
            _log = Logging.CreateLogWriter("writer");
            _itemTimer = new Stopwatch();
            _batchTimer = new Stopwatch();
        }

        public void Write(string collectionName, IEnumerable<IDictionary> data)
        {
            try
            {
                var collectionId = collectionName.ToHash();
                var job = new WriteJob(collectionId, data);

                _writeQueue.Enqueue(job);

                _log.Log(string.Format("enqueued job {0} to be written to {1}", job.Id, collectionName));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("enqueue failed: {0}", ex));

                throw;
            }
        }

        public void Remove(string collectionName, IEnumerable<IDictionary> data)
        {
            //try
            //{
            //    var collectionId = collectionName.ToHash();
            //    var job = new WriteJob(collectionId, data, delete: true);

            //    _writeQueue.Enqueue(job);

            //    _log.Log(string.Format("enqueued job {0} targetting collection {1} ({2})",
            //        job.Id, collectionId, collectionName));
            //}
            //catch (Exception ex)
            //{
            //    _log.Log(string.Format("remove failed: {0}", ex));

            //    throw;
            //}
        }

        private void ExecuteWrite(WriteJob job)
        {
            try
            {
                _itemTimer.Restart();

                using (var session = _sessionFactory.CreateWriteSession(job.CollectionId, _tokenizer))
                {
                    session.Write(job.Documents);
                }

                _log.Log(string.Format("wrote job {0} in {1}",  job.Id, _itemTimer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("failed to execute job {0}: {1}", job.Id, ex));

                throw;
            }
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
            _log.Dispose();
        }
    }
}