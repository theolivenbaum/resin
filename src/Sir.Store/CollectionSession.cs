using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public abstract class CollectionSession : IDisposable
    {
        protected LocalStorageSessionFactory SessionFactory { get; private set; }
        protected string CollectionId { get; }
        protected SortedList<long, VectorNode> Index { get; set; }
        protected Stream ValueStream { get; set; }
        protected Stream KeyStream { get; set; }
        protected Stream DocStream { get; set; }
        protected Stream ValueIndexStream { get; set; }
        protected Stream KeyIndexStream { get; set; }
        protected Stream DocIndexStream { get; set; }
        protected Stream PostingsStream { get; set; }

        public CollectionSession(string collectionId, LocalStorageSessionFactory sessionFactory)
        {
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
        }

        public VectorNode GetIndex(ulong keyHash)
        {
            long keyId;
            if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
            {
                return null;
            }

            return GetIndex(keyId);
        }

        public VectorNode GetIndex(long keyId)
        {
            VectorNode root;

            if (!Index.TryGetValue(keyId, out root))
            {
                return null;
            }

            return root;
        }

        public virtual void Dispose()
        {
            if (ValueStream != null) ValueStream.Dispose();
            if (KeyStream != null) KeyStream.Dispose();
            if (DocStream != null) DocStream.Dispose();
            if (ValueIndexStream != null) ValueIndexStream.Dispose();
            if (KeyIndexStream != null) KeyIndexStream.Dispose();
            if (DocIndexStream != null) DocIndexStream.Dispose();
            if (PostingsStream != null) PostingsStream.Dispose();
        }
    }
}