using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Resin.Documents
{
    public class Document
    {
        public int Id { get; set; }

        public UInt64 Hash { get; set; }

        private readonly IDictionary<string, Field> _fields;

        public IDictionary<string, Field> Fields { get { return _fields; } }

        public Document(int documentId, IList<Field> fields) : this(fields)
        {
            Id = documentId;
        }

        public Document(int documentId, IDictionary<string, Field> fields)
        {
            Id = documentId;
            _fields = fields;
        }

        public Document(IList<Field> fields)
        {
            if (fields == null) throw new ArgumentNullException("fields");

            _fields = fields.ToDictionary(x=>x.Key);
        }

        public IDictionary<short, Field> ToTableRow(IDictionary<string, short> keyIndex)
        {
            return Fields.Values.ToDictionary(field=> keyIndex[field.Key], y => y);
        }

        public override string ToString()
        {
            var output = new StringBuilder();
            foreach(var field in Fields.Values)
            {
                output.AppendLine(field.ToString());
            }
            return output.ToString();
        }
    }
}