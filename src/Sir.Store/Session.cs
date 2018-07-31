using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class Session : IDisposable
    {
        protected readonly string Dir;

        public SessionFactory SessionFactory { get; private set; }
        public ulong CollectionId { get; }
        public SortedList<uint, VectorNode> Index { get; set; }
        public Stream ValueStream { get; set; }
        public Stream KeyStream { get; set; }
        public Stream DocStream { get; set; }
        public Stream ValueIndexStream { get; set; }
        public Stream KeyIndexStream { get; set; }
        public Stream DocIndexStream { get; set; }
        public Stream PostingsStream { get; set; }
        public Stream VectorStream { get; set; }

        public Session(string directory, ulong collectionId, SessionFactory sessionFactory)
        {
            Dir = directory;
            SessionFactory = sessionFactory;
            CollectionId = collectionId;
        }

        public VectorNode GetIndex(ulong key)
        {
            uint keyId;
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
