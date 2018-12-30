using System;
using System.IO;

namespace Sir.Store
{
    public abstract class KeyValueSession : CollectionSession, IDisposable
    {
        protected Stream ValueStream { get; set; }
        protected Stream KeyStream { get; set; }
        protected Stream ValueIndexStream { get; set; }
        protected Stream KeyIndexStream { get; set; }

        protected KeyValueSession(string collectionId, SessionFactory sessionFactory) : base(collectionId, sessionFactory)
        {
        }

        public virtual void Dispose()
        {
            if (ValueStream != null) ValueStream.Dispose();
            if (KeyStream != null) KeyStream.Dispose();
            if (ValueIndexStream != null) ValueIndexStream.Dispose();
            if (KeyIndexStream != null) KeyIndexStream.Dispose();
        }
    }
}