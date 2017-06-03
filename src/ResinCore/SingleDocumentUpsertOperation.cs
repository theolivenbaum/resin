using System.IO;
using System.Linq;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;

namespace Resin
{
    public class SingleDocumentUpsertOperation
    {
        public void Write(
            Document document, 
            DocumentAddressWriter docAddressWriter, 
            DocumentWriter docWriter, 
            Stream docHashesStream,
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

            BlockInfo adr = docWriter.Write(document);                

            docAddressWriter.Write(adr);

            new DocHash(document.Hash).Serialize(docHashesStream);
        }
    }
}