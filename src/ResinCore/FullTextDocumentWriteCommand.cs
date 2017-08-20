using Resin.Analysis;
using Resin.IO;
using log4net;
using Resin.Documents;

namespace Resin
{
    public class FullTextDocumentWriteCommand : IDocumentWriteCommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FullTextDocumentWriteCommand));

        private readonly IAnalyzer _analyzer;
        private readonly TreeBuilder _treeBuilder;

        public FullTextDocumentWriteCommand(IAnalyzer analyzer, TreeBuilder treeBuilder)
        {
            _analyzer = analyzer;
            _treeBuilder = treeBuilder;
        }

        public void Write(DocumentTableRow document, IWriteSession session)
        {
            var analyzedTerms = _analyzer.AnalyzeDocument(document);

            foreach (var term in analyzedTerms)
            {
                _treeBuilder.Add(term.Field, term.Value, term);
            }

            session.Write(document);

            Log.DebugFormat("analyzed doc ID {0}", document.TableId);
        }
    }
}