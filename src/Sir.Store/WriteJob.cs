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
        public bool Executed { get; set; }

        public WriteJob(ulong collectionId, IEnumerable<IDictionary> data)
        {
            CollectionId = collectionId;
            Data = data;
        }

        public void Dispose()
        {
            while (!Executed)
            {
                Thread.Sleep(10);
            }
        }
    }
}
