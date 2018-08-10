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
            using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
            {
                if (job.Remove == null && job.Data != null)
                {
                    session.Write(job.Data, _tokenizer);
                }
                else if (job.Data == null && job.Remove != null)
                {
                    session.Remove(job.Remove, _tokenizer);
                }
                else
                {
                    session.Remove(job.Remove, _tokenizer);
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
