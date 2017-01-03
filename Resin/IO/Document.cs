using System;
using System.Collections.Generic;

namespace Resin.IO
{
    [Serializable]
    public class Document
    {
        public string Id
        {
            get { return Fields["_id"]; }
        }

        private readonly Dictionary<string, string> _fields;

        public Dictionary<string, string> Fields { get { return _fields; } }

        public Document()
        {
            _fields = new Dictionary<string, string>();
        }

        public Document(Dictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }
}