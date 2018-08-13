using System.Collections;
using System.Collections.Generic;

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

        public Writer(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _writeQueue = new ProducerConsumerQueue<WriteJob>(Commit);
        }

        public void Update(string collectionName, IEnumerable<IDictionary> data, IEnumerable<IDictionary> old)
        {
            if (((IList)old).Count == 0)
            {
                old = null;
            }

            _writeQueue.Enqueue(new WriteJob(collectionName.ToHash(), data, old));
        }

        public void Write(string collectionName, IEnumerable<IDictionary> data)
        {
            _writeQueue.Enqueue(new WriteJob(collectionName.ToHash(), data));
        }

        public void Remove(string collectionName, IEnumerable<IDictionary> data)
        {
            _writeQueue.Enqueue(new WriteJob(collectionName.ToHash(), data, delete:true));
        }

        private void Commit(WriteJob job)
        {
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
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
        }
    }
}
