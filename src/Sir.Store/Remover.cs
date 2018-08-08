using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Delete documents from a collection.
    /// </summary>
    public class Remover : IRemover
    {
        public string ContentType => "*";

        private readonly ProducerConsumerQueue<WriteJob> _writeQueue;
        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;

        public Remover(LocalStorageSessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _writeQueue = new ProducerConsumerQueue<WriteJob>(Commit);
        }

        public void Remove(Query query, IReader reader)
        {
            var data = reader.Read(query).ToList();

            using (var job = new WriteJob(query.CollectionId, data, nonBlocking:false))
            {
                _writeQueue.Enqueue(job);
            }
        }

        private void Commit(WriteJob job)
        {
            using (var session = _sessionFactory.CreateWriteSession(job.CollectionId))
            {
                session.Remove(job.Data, _tokenizer);                
            }
            job.Executed = true;
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
        }
    }
}
