using System;

namespace Sir.Store
{
    /// <summary>
    /// Store the location of a value.
    /// </summary>
    public class ValueIndexWriter : IDisposable
    {
        private readonly IKeyValueStore _store;

        public ValueIndexWriter(IKeyValueStore store)
        {
            _store = store;
        }

        public long Append(long offset, int len, byte dataType)
        {
            var offs = Guid.NewGuid().ToHash().MapUlongToLong();
            var buf = new byte[sizeof(long) + sizeof(int) + sizeof(byte)];

            Buffer.BlockCopy(BitConverter.GetBytes(offset), 0, buf, 0, sizeof(long));
            Buffer.BlockCopy(BitConverter.GetBytes(len), 0, buf, sizeof(long), sizeof(int));
            buf[buf.Length - 1] = dataType;

            return offs;
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}
