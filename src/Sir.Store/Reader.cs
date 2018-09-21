using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Query a document collection.
    /// </summary>
    public class Reader : IReader
    {
        public string ContentType => "*";

        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly StreamWriter _log;

        public Reader(LocalStorageSessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _log = Logging.CreateWriter("reader");

        }

        public void Dispose()
        {
            _log.Dispose();
        }

        public IList<IDictionary> Read(Query query, int take, out long total)
        {
            try
            {
                ulong keyHash = query.Term.Key.ToString().ToHash();
                long keyId;

                if (_sessionFactory.TryGetKeyId(keyHash, out keyId))
                {
                    using (var session = _sessionFactory.CreateReadSession(query.CollectionId))
                    {
                        return session.Read(query, take, out total);
                    }
                }

                total = 0;
                return new IDictionary[0];

            }
            catch (Exception ex)
            {
                _log.Log(string.Format("read failed: {0} {1}", query, ex));

                throw;
            }
        }

        public IList<IDictionary> Read(Query query, out long total)
        {
            try
            {
                ulong keyHash = query.Term.Key.ToString().ToHash();
                long keyId;

                if (_sessionFactory.TryGetKeyId(keyHash, out keyId))
                {
                    using (var session = _sessionFactory.CreateReadSession(query.CollectionId))
                    {
                        return session.Read(query, out total);
                    }
                }

                total = 0;
                return new IDictionary[0];
            }
            catch (Exception ex)
            {
                _log.Log(string.Format("read failed: {0} {1}", query, ex));

                throw;
            }
        }
    }
}
