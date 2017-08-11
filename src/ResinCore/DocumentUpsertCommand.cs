using Resin.Analysis;
using Resin.IO;
using DocumentTable;
using log4net;

namespace Resin
{
    public class DocumentUpsertCommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentUpsertCommand));

        private readonly IWriteSession _writeSession;
        private readonly IAnalyzer _analyzer;
        private readonly TreeBuilder _treeBuilder;

        public DocumentUpsertCommand(IWriteSession writeSession, IAnalyzer analyzer, TreeBuilder treeBuilder)
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

            Log.DebugFormat("stored and analyzed doc ID {0}", document.Id);
        }
    }
}