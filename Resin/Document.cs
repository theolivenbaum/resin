using System;
using System.Collections.Generic;
using ProtoBuf;

namespace Resin
{
    [ProtoContract]
    public class Document
    {
        public string Id
        {
            get { return Fields["_id"]; }
        }

        [ProtoMember(1)] 
        private readonly IDictionary<string, string> _fields;

        public IDictionary<string, string> Fields { get { return _fields; } }

        public Document()
        {
            _fields = new Dictionary<string, string>();
        }

        public Document(IDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            _fields = fields;
        }
    }
}