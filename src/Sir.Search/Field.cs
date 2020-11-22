using System;
using System.Diagnostics;

namespace Sir.Search
{
    [DebuggerDisplay("{Key}:{Value}")]
    public class Field
    {
        public long KeyId { get; set; }
        public string Key { get; }
        public object Value { get; set; }
        public bool Index { get; }
        public bool Store { get; }

        public Field(string key, object value, long keyId = -1, bool index = true, bool store = true)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Key = key;
            Value = value;
            Index = index;
            Store = store;
            KeyId = keyId;
        }
    }
}