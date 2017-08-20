using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.Documents;

namespace Resin
{
    public class FullTextUpsertTransaction : DocumentUpsertTransaction
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FullTextUpsertTransaction));

        private readonly IAnalyzer _analyzer;
        private readonly TreeBuilder _treeBuilder;

        public FullTextUpsertTransaction(
            string directory,
            IAnalyzer analyzer,
            Compression compression,
            DocumentStream documents,
            IFullTextWriteSessionFactory writeSessionFactory = null,
            IDocumentWriteCommand documentWriteCommand = null)
            : base(directory, documents)
        {
            _analyzer = analyzer;
            _treeBuilder = new TreeBuilder();

            var factory = writeSessionFactory ?? new FullTextWriteSessionFactory(directory);

            WriteSession = factory.OpenWriteSession(compression, _treeBuilder);

            DocumentWriteCommand = 
                documentWriteCommand ?? new FullTextDocumentWriteCommand(_analyzer, _treeBuilder);
        }
    }
}