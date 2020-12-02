using System;
using System.Diagnostics;

namespace Sir.Search
{
    [DebuggerDisplay("{Key}:{Value}")]
    public class Field
    {
        public long KeyId { get; set; }
        public string Name { get; }
        public object Value { get; set; }
        public bool Index { get; }
        public bool Store { get; }

        public Field(string name, object value, long keyId = -1, bool index = true, bool store = true)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Name = name;
            Value = value;
            Index = index;
            Store = store;
            KeyId = keyId;
        }
    }
}