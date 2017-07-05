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
            IWriteSession writeSession,
            IAnalyzer analyzer,
            TrieBuilder trieBuilder)
        {
            var analyzed = analyzer.AnalyzeDocument(document);

            foreach (var word in analyzed.Words)
            {
                var field = word.Term.Field;
                var token = word.Term.Word.Value;
                var posting = word.Posting;

                trieBuilder.Add(new WordInfo(field, token, posting));
            }

            writeSession.Write(document);
        }
    }
}