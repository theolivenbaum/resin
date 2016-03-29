using System;
using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public string Id
        {
            get { return Fields["id"]; }
        }

        public IDictionary<string, string> Fields { get; set; }

        public static Document FromDictionary(IDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");

            return new Document{Fields = fields};
        }
    }
}