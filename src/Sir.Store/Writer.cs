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

        public void Write(string collectionId, IEnumerable<IDictionary> data)
        {
            var hash = collectionId.ToHash();
            using (var job = new WriteJob(hash, data))
            {
                _writeQueue.Enqueue(job);
            }
        }

        private void Commit(WriteJob tx)
        {
            using (var session = _sessionFactory.CreateWriteSession(tx.CollectionId))
            {
                session.Write(tx.Data, _tokenizer);                
            }
            tx.Executed = true;
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
        }
    }
}
