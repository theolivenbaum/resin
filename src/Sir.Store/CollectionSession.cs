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
        protected Stream VectorStream { get; set; }

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

        public bool KeyExists(ulong keyHash)
        {
            long keyId;
            if (!SessionFactory.TryGetKeyId(keyHash, out keyId))
            {
                return false;
            }
            return true;
        }

        public virtual void Dispose()
        {
            ValueStream.Dispose();
            KeyStream.Dispose();
            DocStream.Dispose();
            ValueIndexStream.Dispose();
            KeyIndexStream.Dispose();
            DocIndexStream.Dispose();

            if (PostingsStream != null) PostingsStream.Dispose();
            if (VectorStream != null) VectorStream.Dispose();
        }
    }
}