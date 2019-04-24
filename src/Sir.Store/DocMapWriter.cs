using System;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Write document maps (key_id/val_id) to the document map store.
    /// </summary>
    public class DocMapWriter : IDisposable
    {
        private readonly IKeyValueStore _store;

        public DocMapWriter(IKeyValueStore store)
        {
            _store = store;

        }

        public (long offset, int length) Append(IList<(long keyId, long valId)> doc)
        {
            var buf = new byte[doc.Count * sizeof(long) * 2];
            var offset = Guid.NewGuid().ToHash().MapUlongToLong();
            var len = buf.Length;

            for (int i = 0; i < doc.Count; i++)
            {
                var pos = i * sizeof(long) * 2;
                var data = doc[i];
                var keyData = BitConverter.GetBytes(data.keyId);
                var valData = BitConverter.GetBytes(data.valId);

                Buffer.BlockCopy(keyData, 0, buf, pos, keyData.Length);
                Buffer.BlockCopy(valData, 0, buf, pos + sizeof(long), valData.Length);
            }

            _store.Put(BitConverter.GetBytes(offset), buf);
            
            return (offset, len);
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}
