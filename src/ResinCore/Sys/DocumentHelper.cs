using System.Collections.Generic;

namespace Resin.Sys
{
    public static class DocumentHelper
    {
        public static DocumentSource ToDocuments(this IEnumerable<dynamic> dynamicDocuments)
        {
            return new InMemoryDocumentSource(dynamicDocuments.ToDocumentsInternal());
        }

        private static IEnumerable<Document> ToDocumentsInternal(this IEnumerable<dynamic> dynamicDocuments)
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