using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public class Document
    {
        public int Id { get; private set; }

        public UInt64 Hash { get; set; }

        private readonly IDictionary<string, Field> _fields;

        public IDictionary<string, Field> Fields { get { return _fields; } }

        public Document(int documentId, IList<Field> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");

            _fields = fields.ToDictionary(x=>x.Key);
            Id = documentId;
        }

        public void Add(string key, object value, bool store = true, bool analyze = true)
        {
            _fields[key] = new Field(Id, key, value, store, analyze);
        }
    }
}