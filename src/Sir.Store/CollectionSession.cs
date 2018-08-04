using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public abstract class CollectionSession : IDisposable
    {
        protected readonly string Dir;

        protected SessionFactory SessionFactory { get; private set; }
        protected ulong CollectionId { get; }
        protected SortedList<long, VectorNode> Index { get; set; }
        protected Stream ValueStream { get; set; }
        protected Stream KeyStream { get; set; }
        protected Stream DocStream { get; set; }
        protected Stream ValueIndexStream { get; set; }
        protected Stream KeyIndexStream { get; set; }
        protected Stream DocIndexStream { get; set; }
        protected Stream PostingsStream { get; set; }
        protected Stream VectorStream { get; set; }

        public CollectionSession(string directory, ulong collectionId, SessionFactory sessionFactory)
        {
            Dir = directory;
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
        }

        public VectorNode GetIndex(ulong key)
        {
            long keyId;
            if (!SessionFactory.TryGetKeyId(key, out keyId))
            {
                return null;
            }
            VectorNode root;
            if (!Index.TryGetValue(keyId, out root))
            {
                return null;
            }
            return root;
        }

        public virtual void Dispose()
        {
        }
    }
}
