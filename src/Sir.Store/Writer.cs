using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Write into a document collection ("table").
    /// </summary>
    public class Writer : IWriter
    {
        public string ContentType => "*";

        private readonly ProducerConsumerQueue<WriteJob> _writeQueue;
        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Stopwatch _timer;

        public Writer(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _writeQueue = new ProducerConsumerQueue<WriteJob>(Commit);
            _log = new StreamWriter(
                File.Open("writer.log", FileMode.Append, FileAccess.Write, FileShare.Read));
            _timer = new Stopwatch();
        }

        public void Update(string collectionName, IEnumerable<IDictionary> data, IEnumerable<IDictionary> old)
        {
            try
            {
                var collectionId = collectionName.ToHash();
                var job = new WriteJob(collectionId, data);

                if (((IList)old).Count == 0)
                {
                    old = null;
                }

                _writeQueue.Enqueue(new WriteJob(collectionName.ToHash(), data, old));

                _log.Log(string.Format("enqueued job {0} targetting collection {1} ({2})",
                    job.Id, collectionId, collectionName));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("update failed: {0}", ex));

                throw;
            }
        }

        public void Write(string collectionName, IEnumerable<IDictionary> data)
        {
            try
            {
                var collectionId = collectionName.ToHash();
                var job = new WriteJob(collectionId, data);

                _writeQueue.Enqueue(job);

                _log.Log(string.Format("enqueued job {0} targetting collection {1} ({2})", 
                    job.Id, collectionId, collectionName));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("write failed: {0}", ex));

                throw;
            }
        }

        public void Remove(string collectionName, IEnumerable<IDictionary> data)
        {
            try
            {
                var collectionId = collectionName.ToHash();
                var job = new WriteJob(collectionId, data, delete: true);

                _writeQueue.Enqueue(job);

                _log.Log(string.Format("enqueued job {0} targetting collection {1} ({2})",
                    job.Id, collectionId, collectionName));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("remove failed: {0}", ex));

                throw;
            }
        }

        private void Commit(WriteJob job)
        {
            try
            {
                _timer.Restart();

                if (job.Remove == null && job.Data != null)
                {
                    using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                    {
                        session.Write(job.Data, _tokenizer);
                    }
                }
                else if (job.Data == null && job.Remove != null)
                {
                    using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                    {
                        session.Remove(job.Remove, _tokenizer);
                    }
                }
                else
                {
                    using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                    {
                        session.Remove(job.Remove, _tokenizer);
                    }
                    using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
                    {
                        session.Write(job.Data, _tokenizer);
                    }
                }

                _log.Log(string.Format("wrote job {0} to {1} in {2}",
                    job.Id, job.CollectionId, _timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("failed to commit job {0}: {1}", job.Id, ex));

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
