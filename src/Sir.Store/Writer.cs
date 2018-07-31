using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
    public class Writer : IWriter
    {
        public string ContentType => "*";

        private readonly ProducerConsumerQueue<WriteTransaction> _writeQueue;
        private readonly SessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;

        public Writer(SessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
            _writeQueue = new ProducerConsumerQueue<WriteTransaction>(Commit);
        }

        public void Write(string collectionId, IEnumerable<IDictionary> data)
        {
            using (var tx = new WriteTransaction(collectionId.ToHash(), data))
            {
                _writeQueue.Enqueue(tx);
            }
        }

        private void Commit(WriteTransaction tx)
        {
            using (var session = _sessionFactory.CreateWriteSession(tx.CollectionId))
            {
                session.Write(tx.Data, _tokenizer);                
            }
            tx.Committed = true;
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
        }
    }
}
