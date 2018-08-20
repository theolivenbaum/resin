using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Read from the document store.
    /// </summary>
    public class Reader : IReader
    {
        public string ContentType => "*";

        private readonly LocalStorageSessionFactory _sessionFactory;
        private readonly StreamWriter _log;

        public Reader(LocalStorageSessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _log = new StreamWriter(
                File.Open("reader.log", FileMode.Append, FileAccess.Write, FileShare.Read));
        }

        public void Dispose()
        {
            _log.Dispose();
        }

        public IEnumerable<IDictionary> Read(Query query)
        {
            try
            {
                ulong keyHash = query.Term.Key.ToString().ToHash();
                long keyId;

                if (_sessionFactory.TryGetKeyId(keyHash, out keyId))
                {
                    using (var session = _sessionFactory.CreateReadSession(query.CollectionId))
                    {
                        return session.Read(query).ToList();
                    }
                }

                return Enumerable.Empty<IDictionary>();

            }
            catch (Exception ex)
            {
                _log.Log(string.Format("read failed: {0} {1}", query, ex));

                throw;
            }
            
        }
    }
}
