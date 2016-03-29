using System;
using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public int Id { get; set; }
        public IDictionary<string, string> Fields { get; set; }

        public static Document FromDictionary(int docId, IDictionary<string, string> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");

            return new Document{Id = docId, Fields = fields};
        }
    }
}