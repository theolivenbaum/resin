using System;

namespace Resin.IO
{
    public struct Field
    {
        public string Key { get; private set; }
        public string Value { get; private set; }
        public bool Store { get; private set; }
        public bool Analyze { get; private set; }

        public Field(string key, string value, bool store = true, bool analyze = true)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (value == null) throw new ArgumentNullException("value");

            Key = key;
            Value = value;
            Store = store;
            Analyze = analyze;
        }
    }
}