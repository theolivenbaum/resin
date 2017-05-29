using System;
using System.Diagnostics;

namespace Resin
{
    [DebuggerDisplay("{Value}")]
    public struct Field
    {
        private readonly string _value;

        public string Value { get { return _value; } }
        
        public string Key { get; private set; }
        public bool Store { get; private set; }
        public bool Analyze { get; private set; }

        public Field(string key, object value, bool store = true, bool analyze = true)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key");
            if (value == null) throw new ArgumentNullException("value");

            Key = key;
            Store = store;
            Analyze = analyze;

            if (value is DateTime)
            {
                _value = ((DateTime)value).Ticks.ToString();
            }
            else
            {
                _value = value.ToString(); 
            }
        }
    }
}