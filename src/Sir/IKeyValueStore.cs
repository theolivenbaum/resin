using System.Collections.Generic;

namespace Sir
{
    public interface IKeyValueStore
    {
        void Put(byte[] key, byte[] value);
        byte[] Get(byte[] key);
        IEnumerable<KeyValuePair<byte[], byte[]>> GetMany(byte[][] keys);
    }
}