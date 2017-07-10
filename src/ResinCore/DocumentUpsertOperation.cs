using System.Linq;
using Resin.Analysis;
using Resin.IO;
using DocumentTable;

namespace Resin
{
    public class DocumentUpsertOperation
    {
        private readonly IWriteSession _writeSession;
        private readonly IAnalyzer _analyzer;
        private readonly TrieBuilder _treeBuilder;

        public DocumentUpsertOperation(IWriteSession writeSession, IAnalyzer analyzer, TrieBuilder treeBuilder)
        {
            _writeSession = writeSession;
            _analyzer = analyzer;
            _treeBuilder = treeBuilder;
        }

        public void Write(Document document)
        {
            var analyzed = _analyzer.AnalyzeDocument(document);

            foreach (var word in analyzed.Words)
            {
                var field = word.Term.Field;
                var token = word.Term.Word.Value;
                var posting = word.Posting;

                _treeBuilder.Add(new WordInfo(field, token, posting));
            }

            _writeSession.Write(document);
        }
    }
}