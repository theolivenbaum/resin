using System.Collections.Generic;

namespace Resin
{
    public class Document
    {
        public int Id { get; set; }
        public Dictionary<string, List<string>> Fields { get; set; }

        public static Document FromDictionary(int docId, Dictionary<string, List<string>> fields)
        {
            return new Document{Id = docId, Fields = fields};
        }
    }

    public class DocumentInfo
    {
        public int Id { get; set; }
        public IDictionary<string, FieldInfo> Fields { get; set; }

    }

    public class FieldInfo
    {
        public int FieldId { get; set; }
        public IList<string> Values { get; set; }
    }

    public class FieldFileEntry
    {
        public int DocId { get; set; }
        public int FieldId { get; set; }
        public string Value { get; set; }
    }
}