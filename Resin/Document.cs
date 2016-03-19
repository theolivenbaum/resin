using System;
using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public int Id { get; set; }
        public Dictionary<string, List<string>> Fields { get; set; }

        public static Document FromDictionary(int docId, Dictionary<string, List<string>> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");

            return new Document{Id = docId, Fields = fields};
        }
    }
}