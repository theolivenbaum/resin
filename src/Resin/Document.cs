using System;
using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public int Id { get; set; }

        private readonly IDictionary<string, string> _fields;

        public IDictionary<string, string> Fields { get { return _fields; } }

        public Document(IDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }
}