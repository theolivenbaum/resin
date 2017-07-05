using System.Linq;
using Resin.Analysis;
using Resin.IO;
using DocumentTable;

namespace Resin
{
    public class DocumentUpsertOperation
    {
        public void Write(
            Document document,
            IDocumentStoreWriter storeWriter,
            IAnalyzer analyzer,
            TrieBuilder trieBuilder)
        {
            var analyzed = analyzer.AnalyzeDocument(document);

            foreach (var term in analyzed.Words.GroupBy(t => t.Term.Field))
            {
                trieBuilder.Add(term.Key, term.Select(t =>
                {
                    var field = t.Term.Field;
                    var token = t.Term.Word.Value;
                    var posting = t.Posting;
                    return new WordInfo(field, token, posting);
                }).ToList());
            }

            storeWriter.Write(document);
        }
    }
}