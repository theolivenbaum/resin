using Resin.Documents;
using Resin.Sys;
using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public static class Helper
    {
        public static IDictionary<string, short> ToKeyIndex(this DocumentTableRow document)
        {
            var keys = document.Fields.Keys.ToList();
            var keyIndex = new Dictionary<string, short>();

            for (int i = 0; i < keys.Count; i++)
            {
                keyIndex.Add(keys[i], (short)i);
            }

            return keyIndex;
        }

        public static DocumentStream ToDocuments(
            this IEnumerable<dynamic> dynamicDocuments, string primaryKeyFieldName = null)
        {
            return new InMemoryDocumentStream(
                dynamicDocuments.ToDocumentsInternal(), 
                primaryKeyFieldName);
        }

        private static IEnumerable<DocumentTableRow> ToDocumentsInternal(
            this IEnumerable<dynamic> dynamicDocuments, string primaryKeyFieldName = null)
        {
            foreach (var dyn in dynamicDocuments)
            {
                var fields = new List<Field>();

                foreach (var field in Util.ToDictionary(dyn))
                {
                    fields.Add(new Field(field.Key, field.Value));
                }
                yield return new DocumentTableRow(fields);
            }
        }
    }
}