using System.Collections.Generic;

namespace Resin.Sys
{
    public static class DocumentHelper
    {
        public static DocumentStream ToDocuments(
            this IEnumerable<dynamic> dynamicDocuments, string primaryKeyFieldName = null)
        {
            return new InMemoryDocumentStream(
                dynamicDocuments.ToDocumentsInternal(), 
                primaryKeyFieldName);
        }

        private static IEnumerable<Document> ToDocumentsInternal(
            this IEnumerable<dynamic> dynamicDocuments, string primaryKeyFieldName = null)
        {
            foreach (var dyn in dynamicDocuments)
            {
                var fields = new List<Field>();

                foreach (var field in Util.ToDictionary(dyn))
                {
                    fields.Add(new Field(field.Key, field.Value));
                }
                yield return new Document(fields);
            }
        }
    }
}