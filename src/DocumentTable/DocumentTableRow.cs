using System.Collections.Generic;

namespace DocumentTable
{
    public class DocumentTableRow
    {
        public IDictionary<short, Field> Fields { get; private set; }

        public DocumentTableRow(IDictionary<short, Field> fields)
        {
            Fields = fields;
        }
    }
}