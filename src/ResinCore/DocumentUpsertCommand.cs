using Resin.Analysis;
using Resin.IO;
using DocumentTable;

namespace Resin
{
    public class DocumentUpsertCommand
    {
        private readonly IWriteSession _writeSession;
        private readonly IAnalyzer _analyzer;
        private readonly TrieBuilder _treeBuilder;

        public DocumentUpsertCommand(IWriteSession writeSession, IAnalyzer analyzer, TrieBuilder treeBuilder)
        {
            _writeSession = writeSession;
            _analyzer = analyzer;
            _treeBuilder = treeBuilder;
        }

        public void Write(Document document)
        {
            var analyzedTerms = _analyzer.AnalyzeDocument(document);

            foreach (var word in analyzedTerms)
            {
                var field = word.Term.Field;
                var token = word.Term.Word.Value;
                var postings = word.Postings;

                _treeBuilder.Add(field, token, postings);
            }

            _writeSession.Write(document);
        }
    }
}