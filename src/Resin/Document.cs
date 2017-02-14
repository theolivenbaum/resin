using System;
using System.Collections.Generic;

namespace Resin
{
    [Serializable]
    public class Document
    {
        /// <summary>
        /// Get the value of the "_id" field.
        /// </summary>
        public string Id
        {
            get { return Fields["_id"]; }
        }

        private readonly IDictionary<string, string> _fields;

        public IDictionary<string, string> Fields { get { return _fields; } }

        public Document():this(new Dictionary<string, string>())
        {
        }

        public Document(IDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }
}