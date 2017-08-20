using System;
using System.Diagnostics;

namespace Resin.Documents
{
    [DebuggerDisplay("{Value}")]
    public class Field
    {
        private readonly string _value;

        public string Value { get { return _value; } }
        
        public string Key { get; private set; }
        public bool Store { get; private set; }
        public bool Analyze { get; private set; }
        public bool Index { get; private set; }

        public Field(string key, object value, bool store = true, bool analyze = true, bool index = true)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key");
            if (value == null) throw new ArgumentNullException("value");

            Key = key;
            Store = store;
            Analyze = analyze;
            Index = index;

            object obj = value;

            if (value is DateTime)
            {
                obj = ((DateTime)value).ToUniversalTime().Ticks;
            }

            if (obj is string)
            {
                _value = (string)obj;
            }
            else 
            {
                // Assumes all values that are not DateTime or string must be Int64.

                // TODO: implement native number indexes

                _value = obj.ToString().PadLeft(19, '0');
            }
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, Value);
        }
    }
}