using System;
using System.Collections.Generic;

namespace Resin.IO
{
    public class Document
    {
        public int Id { get; set; }
        public UInt64 Hash { get; set; }

        private readonly IList<Field> _fields;

        public IList<Field> Fields { get { return _fields; } }

        public Document(IList<Field> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }

    public struct Field
    {
        public string Key { get; private set; }
        public string Value { get; private set; }
        public bool Store { get; private set; }

        public Field(string key, string value, bool store = true)
        {
            Key = key;
            Value = value;
            Store = store;
        }
    }
}