using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Sir.Store
{
    public class WriteJob : IDisposable
    {
        public ulong CollectionId { get; }
        public IEnumerable<IDictionary> Data { get; private set; }

        private readonly bool _nonBlocking;

        public bool Executed { get; set; }

        public WriteJob(ulong collectionId, IEnumerable<IDictionary> data, bool nonBlocking = true)
        {
            CollectionId = collectionId;
            Data = data;
            _nonBlocking = nonBlocking;
        }

        public void Dispose()
        {
            if (_nonBlocking) return;

            while (!Executed)
            {
                Thread.Sleep(10);
            }
        }
    }
}
